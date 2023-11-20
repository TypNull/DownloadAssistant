using DownloadAssistant.Base;
using DownloadAssistant.Options;
using DownloadAssistant.Utilities;
using Requests;
using Requests.Options;

namespace DownloadAssistant.Request
{
    /// <summary>
    /// A <see cref="WebRequest{TOptions, TCompleated}"/> that loads the response as stream and saves it to a file
    /// </summary>
    public class LoadRequest : WebRequest<LoadRequestOptions, string>, IProgressableRequest
    {
        private int _attemptCounter = 0;
        private bool _isCleared = false;
        private ChunkHandler _chunkHandler = new();
        private IRequest _request = null!;


        /// <summary>
        /// Range that should be downloaded.
        /// </summary>
        public LoadRange Range => Options.Range;

        /// <inheritdoc/>
        public override Task Task => _request.Task;

        /// <summary>
        /// Filename of the content that is downloaded
        /// </summary>
        public string? Filename { get; private set; }

        /// <summary>
        /// Bytes that are written to the temp file
        /// </summary>
        public long BytesWritten => IsChunked ? _chunkHandler.BytesWritten : ((GetRequest)_request).BytesWritten;

        /// <summary>
        /// Bytes that are downloaded
        /// </summary>
        public long BytesDownloaded => IsChunked ? _chunkHandler.BytesDownloaded : ((GetRequest)_request).BytesWritten;

        /// <summary>
        /// <see cref="AggregateException"/> that contains the throwed exeptions
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
                    return _chunkHandler.Requests.Sum(x => x.PartialContentLegth ?? 0);
                else
                    return ((GetRequest)_request).ContentLength;
            }
        }

        /// <inheritdoc/>
        public override int AttemptCounter => _attemptCounter;

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
        public string TempDestination { get; private set; } = string.Empty;

        /// <summary>
        /// Progress to get updates of the download process.
        /// </summary>
        public Progress<float> Progress => ((IProgressableRequest)_request).Progress;

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
            GetRequestOptions options = Options.ToGetRequestOptions() with { InfosFetched = OnInfosFetched };

            if (IsChunked)
            {
                _request = _chunkHandler.RequestContainer;
                _request.StateChanged += OnStateChanged;
                _chunkHandler.Add(CreateChunk(0, options));

                Task.Run(() =>
                {
                    for (int i = 1; i < Options.Chunks; i++)
                        _chunkHandler.Add(CreateChunk(i, options));
                    AutoStart();
                });
                return;
            }

            options = options with
            {
                Filename = $"{(string.IsNullOrWhiteSpace(Options.Filename) ? "*.*" : Options.Filename)}.part",
                RequestFailed = OnFailure,
                RequestStarted = Options.RequestStarted,
                RequestCompleated = OnCompletion,
            };

            _request = new GetRequest(Url, options);
            _request.StateChanged += OnStateChanged;
            AutoStart();
        }

        private GetRequest CreateChunk(int index, GetRequestOptions options)
        {
            options = options with
            {
                Range = new LoadRange(new Index(index), Options.Chunks),
                Filename = $"{(string.IsNullOrWhiteSpace(Options.Filename) ? "*.*" : Options.Filename)}.{1 + index}_chunk",
                RequestCompleated = OnCompletion,
                RequestFailed = OnFailure,
            };
            if (index == 0)
                options.RequestStarted += Options.RequestStarted;

            return new GetRequest(Url, options);
        }

        private void OnStateChanged(object? sender, RequestState state)
        {
            if (IsChunked && _request.State == RequestState.Compleated)
                State = RequestState.Running;
            else if (State == RequestState.Idle)
                State = _request.State;
        }


        private void OnFailure(IRequest? request, HttpResponseMessage? element)
        {
            State = RequestState.Failed;
            Pause();
            ClearOnFailure();
        }

        private void OnInfosFetched(GetRequest? request)
        {
            if (request == null)
                return;

            if (IsChunked)
                _chunkHandler.SetInfos(request);
            if (Filename == null)
            {
                Filename = request.ContentName;
                Destination = Path.Combine(Options.DestinationPath, Filename);
                TempDestination = Path.Combine(Options.TempDestination, Filename + ".part");
                SynchronizationContext.Post((o) => Options.InfosFetched?.Invoke((LoadRequest)o!), this);
                ExcludedExtensions(request.ContentExtension);
            }
        }

        private void ExcludedExtensions(string contentExtension)
        {
            if (Options.ExcludedExtensions != null && !string.IsNullOrWhiteSpace(contentExtension) && Options.ExcludedExtensions.Any(contentExtension.EndsWith))
            {
                AddException(new InvalidOperationException($"Content extension is invalid"));
                OnFailure(this, null);
            }
        }

        private async void OnCompletion(IRequest? request, string? _)
        {
            if (request == null)
                return;
            bool canMove = false;
            if (IsChunked)
            {
                if (Options.MergeWhileProgress || _chunkHandler.Requests.All(request => request.State == RequestState.Compleated))
                    canMove = await _chunkHandler.StartMergeTo(TempDestination);
            }
            else
                canMove = true;

            if (canMove)
                TempToDestination();
        }

        private void TempToDestination()
        {
            IOManager.Move(TempDestination, Destination);
            ((IProgress<float>)Progress).Report(1f);
            State = RequestState.Compleated;
            SynchronizationContext.Post((o) => Options.RequestCompleated?.Invoke((IRequest)o!, Destination), this);
        }

        private void ClearOnFailure()
        {
            if (!Options.DeleteTmpOnFailure || _isCleared)
                return;
            _isCleared = true;
            if (File.Exists(TempDestination))
                File.Delete(TempDestination);
            if (File.Exists(Destination) && new FileInfo(Destination).Length == 0)
                File.Delete(Destination);
            _chunkHandler.DeleteChunks();
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
        public override void Start()
        {
            State = RequestState.Idle;
            _request.Start();
        }

        /// <summary>
        /// Set the <see cref="LoadRequest"/> on hold.
        /// </summary>
        public override void Pause()
        {
            base.Pause();
            _request.Pause();
        }
        /// <inheritdoc/>
        public override void Cancel()
        {
            base.Cancel();
            _request.Cancel();
        }

        /// <inheritdoc/>
        public override void Wait() => Task.Wait();

        ///<inheritdoc/>
        public override void Dispose()
        {
            _request.Dispose();
            base.Dispose();
        }
    }
}
