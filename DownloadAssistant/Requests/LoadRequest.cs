using DownloadAssistant.Base;
using DownloadAssistant.Options;
using DownloadAssistant.Utilities;
using Requests;
using Requests.Options;
using System.Diagnostics;

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
        /// Bytes that are written to the temp file
        /// </summary>
        public long BytesWritten => _chunkHandler.BytesWritten;

        /// <summary>
        /// Bytes that are downloaded
        /// </summary>
        public long BytesDownloaded => _chunkHandler.BytesDownloaded;

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
        private int _attemptCounter = 0;

        /// <summary>
        /// If this <see cref="IRequest"/> downloads in parts
        /// </summary>
        public bool IsChunked { get; private set; }

        private ChunkHandler _chunkHandler = new();

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
        public Progress<float> Progress => ((IProgressable)_request).Progress;

        private IRequest _request = null!;

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
            GetRequestOptions options = Options.ToGetRequestOptions() with { InfosFetched = OnInfosFetched };

            if (IsChunked)
            {
                _chunkHandler = new ChunkHandler();
                _request = _chunkHandler.RequestContainer;


                for (int i = 0; i < Options.Chunks; i++)
                    _chunkHandler.Add(CreateChunk(i, options));
                
                return;
            }

            options = options with
            {
                Filename = $"{(string.IsNullOrWhiteSpace(Options.Filename) ? "*" : Options.Filename)}.part",
            };
            options.RequestStarted += Options.RequestStarted;
            options.RequestFailed += (message) => _attemptCounter++;
            options.RequestCompleated += MoveTemp;
            _request = new GetRequest(Url, options)
            {
                StateChanged = OnStateChanged
            };
        }

        private void OnStateChanged(Request<GetRequestOptions, GetRequest, HttpResponseMessage?>? req)
        {
            if (req?.State == RequestState.Failed || req?.State == RequestState.Cancelled)
                ClearOnFailure();
        }

        private GetRequest CreateChunk(int index, GetRequestOptions options)
        {
            options = options with
            {
                Range = new LoadRange(new Index(index), Options.Chunks),
                Filename = $"{(string.IsNullOrWhiteSpace(Options.Filename) ? "*" : Options.Filename)}.{1 + index}_chunk",
                RequestCompleated = MoveTemp,
                RequestFailed = OnFailure,
            };
            if (index == 0)
                options.RequestStarted += Options.RequestStarted;

            options.RequestFailed += (message) => _attemptCounter++;
           
            return new GetRequest(Url, options);
        }

        private void OnFailure(HttpResponseMessage? element)
        {
            if (State != RequestState.Failed)
                return;
            Cancel();
            ClearOnFailure();
        }

        private void OnInfosFetched(GetRequest? request)
        {
            if (request == null)
                return;

            
            if (IsChunked)
            {
                _chunkHandler.SetInfos(request);
                if (Destination == string.Empty)
                {
                    Destination = Path.Combine(Options.DestinationPath, request.ContentName);
                    TempDestination = Path.Combine(Options.TempDestination, request.ContentName + ".part");
                }
                return;
            }
            Destination = Path.Combine(Options.DestinationPath, request.ContentName);
            TempDestination = request.FilePath;


            ExcludedExtensions(request.ContentExtension);
        }

        private void ExcludedExtensions(string contentExtension)
        {
            if  (Options.ExcludedExtensions != null && !string.IsNullOrWhiteSpace(contentExtension) && Options.ExcludedExtensions.Any(contentExtension.EndsWith))
            {
                AddException(new InvalidOperationException($"Content extension is invalid"));
                Cancel();
                _state = RequestState.Failed;
            }
        }

        private async void MoveTemp(GetRequest? getRequest)
        {
            if (getRequest == null)
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
            _state = RequestState.Compleated;
            Options.RequestCompleated?.Invoke(Destination);
        }

        private bool _cleared = false;
        private void ClearOnFailure()
        {
            if (!Options.DeleteTmpOnFailure || _cleared)
                return;
            _cleared = true;
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

        public override void Start() => _request.Start();

        /// <summary>
        /// Set the <see cref="LoadRequest"/> on hold.
        /// </summary>
        public override void Pause() => _request.Pause();
        /// <inheritdoc/>
        public override void Cancel() =>_request.Cancel();  
        ///<inheritdoc/>
        public override void Dispose() => _request.Dispose();
    }
}
