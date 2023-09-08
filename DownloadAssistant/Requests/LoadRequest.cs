using DownloadAssistant.Base;
using DownloadAssistant.Options;
using Requests;
using Requests.Options;

namespace DownloadAssistant.Request
{
    /// <summary>
    /// A <see cref="WebRequest{TOptions, TCompleated}"/> that loads the response as stream and saves it to a file
    /// </summary>
    public class LoadRequest : WebRequest<LoadRequestOptions, string>, IProgressable, IRequest
    {
        /// <summary>
        /// Range that should be downloaded.
        /// </summary>
        public LoadRange Range => Options.Range;

        /// <inheritdoc/>
        public override RequestState State
        {
            get
            {
                if (_state == RequestState.Available)
                    return _request.State;
                return _state;
            }
        }
        private RequestState _state = RequestState.Available;

        /// <summary>
        /// Bytes that are written to the destination file
        /// </summary>
        public long BytesWritten { get; private set; } = 0;

        /// <summary>
        /// Bytes that are written to the temp or chunk file
        /// </summary>
        public long BytesDownloaded { get; private set; } = 0;

        /// <summary>
        /// <see cref="AggregateException"/> that contains the throwed Exeptions
        /// </summary>
        public override AggregateException? Exception
        {
            get
            {
                if (base.Exception != null && _request.Exception != null)
                    return new(base.Exception!, _request.Exception!);
                else if (base.Exception != null)
                    return base.Exception;
                return _request.Exception;
            }
        }

        /// <summary>
        /// Length of the content that will be downloaded
        /// </summary>
        public long ContentLength
        {
            get
            {
                if (IsChunked)
                    return _progressCon.GetRequests().Sum(x => x.PartialContentLegth ?? 0);
                else
                    return ((GetRequest)_request).ContentLength;
            }
        }

        private int _isCopying = 0;


        private bool _infoFetched = false;

        /// <inheritdoc/>
        public override int AttemptCounter => _attemptCounter;
        private int _attemptCounter = 0;

        /// <summary>
        /// If this <see cref="IRequest"/> downloads in parts
        /// </summary>
        public bool IsChunked { get; private set; }

        /// <summary>
        /// Path to the download file
        /// </summary>
        public string Destination { get; private set; } = string.Empty;

        /// <summary>
        /// Path to the temporary created file
        /// </summary>
        public string TmpDestination { get; private set; } = string.Empty;

        /// <summary>
        /// Progress to get updates of the download process.
        /// </summary>
        public Progress<float> Progress => ((IProgressable)_request).Progress;

        private readonly ProgressableContainer<GetRequest> _progressCon = new();
        private List<GetRequest> _copied = new();
        private IRequest _request = null!;

        private string _tempExt = ".part";

        /// <summary>
        /// Creates a <see cref="IRequest"/> that can download a file to a temp file
        /// </summary>
        /// <param name="url">URL of the file</param>
        /// <param name="options">Options to costumize</param>
        /// <exception cref="IndexOutOfRangeException">Throws an exception when the Range is not set right</exception>
        /// <exception cref="NotSupportedException">Throws an exception when WriteMode is not set right</exception>
        public LoadRequest(string url, LoadRequestOptions? options) : base(url, options)
        {
            if (Range.Start >= Range.End)
                throw new IndexOutOfRangeException(nameof(Range.Start) + " has to be less than " + nameof(Range.End));
            if (Range.Start != null && Options.WriteMode == WriteMode.Append)
                throw new NotSupportedException($"Can not set {nameof(WriteMode.Append)} if {nameof(Range.Start)} is not null");


            if (Options.Chunks > 1)
                IsChunked = true;

            CreateDirectory();
            CreateRequest();
            AutoStart();
        }

        /// <summary>
        /// Creates requestet files and sets them to Options
        /// </summary>
        private void CreateDirectory()
        {
            Directory.CreateDirectory(Options.DestinationPath);
            if (!string.IsNullOrWhiteSpace(Options.TempDestination))
                Directory.CreateDirectory(Options.TempDestination);
            else
                Options = Options with { TempDestination = Options.DestinationPath };
        }

        private void CreateRequest()
        {
            GetRequestOptions options = Options.ToGetRequestOptions() with
            {
                InfosFetched = OnInfosFetched
            };

            if (IsChunked)
            {
                _request = _progressCon;
                _tempExt = "_chunk";
                for (int i = 0; i < Options.Chunks; i++)
                    _progressCon.Add(CreateChunk(i, options));
            }
            else
            {
                options = options with
                {
                    Filename = $"{(string.IsNullOrWhiteSpace(Options.Filename) ? "*" : Options.Filename)}{_tempExt}",
                };
                options.RequestStarted += Options.RequestStarted;
                options.RequestFailed += (message) => _attemptCounter++;
                options.RequestCompleated += CopyOrMerge;
                _request = new GetRequest(Url, options);
            }
        }


        private GetRequest CreateChunk(int index, GetRequestOptions options)
        {
            options = options with
            {
                Range = new LoadRange(new Index(index), Options.Chunks),
                Filename = $"{(string.IsNullOrWhiteSpace(Options.Filename) ? "*" : Options.Filename)}.{1 + index}{_tempExt}"
            };
            if (index == 0)
                options.RequestStarted += Options.RequestStarted;

            options.RequestFailed += (message) => _attemptCounter++;
            options.RequestCompleated += CopyOrMerge;
            return new GetRequest(Url, options);
        }

        private void OnInfosFetched(GetRequest? request)
        {
            if (request == null || _infoFetched)
                return;
            _infoFetched = true;

            Destination = Path.Combine(Options.DestinationPath, request!.Filename.Remove(request.Filename.LastIndexOf('.')));
            TmpDestination = request.FilePath;
            LoadBytesWritten();

            _progressCon.GetRequests().ToList().ForEach(x => x.SetContentLength(request.FullContentLegth!.Value));

            PreAllocateFileLength(request.FullContentLegth!.Value);
            ExcludedExtensions(request.ContentExtension);
        }

        private void LoadBytesWritten()
        {
            throw new NotImplementedException();
        }

        private void ExcludedExtensions(string contentExtension)
        {
            if (Options.ExcludedExtensions.Any(contentExtension.EndsWith))
            {
                AddException(new InvalidOperationException($"Content extension is invalid"));
                Cancel();
            }
        }

        private void PreAllocateFileLength(long value)
        {
            if (!Options.PreAllocateFileLength)
                return;
            FileStream fileStream = File.Create(Destination);
            fileStream.SetLength(value);
            fileStream.Close();
        }

        private async void CopyOrMerge(GetRequest? getRequest)
        {
            if (getRequest == null)
                return;

            if (IsChunked)
            {
                if (Options.MergeWhileProgress || _progressCon.GetRequests().All(request => request.State == RequestState.Compleated))
                    await MergeChunks();
            }
            else
            {
                IOManager.Move(((GetRequest)_request).FilePath, Destination);
                BytesWritten = BytesDownloaded;
                ((IProgress<float>)Progress).Report(1f);
                Options.RequestCompleated?.Invoke(Destination);
            }
        }

        /// <summary>
        /// Merges all chunked parts of a file into one big file.
        /// </summary>
        /// <returns>A awaitable Task</returns>
        public async Task MergeChunks()
        {
            if (Interlocked.CompareExchange(ref _isCopying, 0, 1) == 1)//If is Copying
                return;
            IReadOnlyList<GetRequest> requests = _progressCon.GetRequests();
            //Check if the fist part was downloaded
            if (requests[0].State != RequestState.Compleated)
            {
                _isCopying = 0;
                return;
            }

            //FileStream to merge the chunked files
            FileStream? outputStream = null;
            try
            {
                outputStream = new(Destination, FileMode.Open)
                {
                    Position = BytesWritten
                };
                for (int i = 0; i < requests.Count; i++)
                {
                    if (requests[i].State != RequestState.Compleated)
                        break;
                    if (_copied.Contains(requests[i]))
                        continue;
                    string path = requests[i].FilePath;
                    if (!File.Exists(path))
                        break;

                    FileStream inputStream = File.OpenRead(path);
                    await inputStream.CopyToAsync(outputStream);
                    BytesWritten += inputStream.Length;
                    await inputStream.FlushAsync();
                    await inputStream.DisposeAsync();
                    File.Delete(path);
                    _copied.Add(requests[i]);
                }
            }
            catch (Exception) { }
            finally
            {
                if (outputStream != null)
                {
                    await outputStream.FlushAsync();
                    await outputStream.DisposeAsync();
                }
                _isCopying = 0;
            }
        }


        async Task IRequest.StartRequestAsync() => await RunRequestAsync();

        /// <inheritdoc/>
        protected override async Task<RequestReturn> RunRequestAsync()
        {
            await Task.Yield();
            Start();
            return new()
            {
                Successful = true
            };
        }

        /// <summary>
        /// Start the <see cref="LoadRequest"/> if it is not yet started or paused.
        /// </summary>

        public override void Start() => _request.Start();

        /// <summary>
        /// Set the <see cref="LoadRequest"/> on hold.
        /// </summary>
        public override void Pause() => _request.Pause();
        /// <inheritdoc/>
        public override void Cancel() => _request.Cancel();
        ///<inheritdoc/>
        public override void Dispose() => _request.Dispose();
    }
}
