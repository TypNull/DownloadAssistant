﻿using DownloadAssistant.Base;
using DownloadAssistant.Options;
using DownloadAssistant.Utilities;
using Requests;
using Requests.Options;

namespace DownloadAssistant.Requests
{
    /// <summary>
    /// Represents a <see cref="WebRequest{TOptions, TCompleated}"/> that loads a response as a stream and saves it to a file.
    /// </summary>
    public class LoadRequest : WebRequest<LoadRequestOptions, string>, IProgressableRequest, ISpeedReportable
    {
        /// <summary>
        /// Indicates whether the request has been cleared.
        /// </summary>
        private bool _isCleared = false;

        /// <summary>
        /// Handles the chunks of data during the download process.
        /// </summary>
        private readonly ChunkHandler _chunkHandler = new();

        /// <summary>
        /// This property exposes the inner <see cref="IRequest"/> instance, which can be either a <see cref="GetRequest"/> 
        /// or an <see cref="ExtendedContainer{GetRequest}"/> with <see cref="GetRequest"/>'s.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>WARNING:</b> Modifying this object can lead 
        /// to unexpected behavior and is not recommended. Incorrect usage can result in data corruption, incomplete downloads, 
        /// or other issues! For most use cases, use higher-level methods and properties 
        /// provided by the <see cref="LoadRequest"/> class to avoid potential pitfalls.
        /// </para>
        /// </remarks>
        public IRequest InnerRequest { get; protected set; } = null!;

        /// <summary>
        /// Specifies the mode of writing to the file.
        /// </summary>
        private WriteMode _writeMode;

        /// <summary>
        /// An internal check variable to prevent parallel execution.
        /// </summary>
        private int _check = 0;

        /// <summary>
        /// Gets the range of data that should be downloaded.
        /// </summary>
        public LoadRange Range => Options.Range;

        /// <inheritdoc/>
        public override Task Task => InnerRequest.Task;

        /// <summary>
        /// Gets the filename of the content that is being downloaded.
        /// </summary>
        public string? Filename { get; private set; }

        /// <summary>
        /// Gets the current transfer rate in bytes per second.
        /// </summary>
        public long CurrentBytesPerSecond => IsChunked ? _chunkHandler.RequestContainer.Sum(x => x.CurrentBytesPerSecond) : ((GetRequest)InnerRequest).CurrentBytesPerSecond;

        /// <summary>
        /// Gets the number of bytes that have been written to the temporary file.
        /// </summary>
        public long BytesWritten => IsChunked ? _chunkHandler.BytesWritten : ((GetRequest)InnerRequest).BytesWritten;

        /// <summary>
        /// Gets the number of bytes that have been downloaded.
        /// </summary>
        public long BytesDownloaded => IsChunked ? _chunkHandler.BytesDownloaded : ((GetRequest)InnerRequest).BytesWritten;

        /// <summary>
        /// Gets the <see cref="AggregateException"/> that contains the exceptions thrown during the request.
        /// </summary>
        public override AggregateException? Exception
        {
            get
            {
                if (base.Exception != null && InnerRequest.Exception != null)
                    return new(base.Exception!, InnerRequest.Exception!);
                else if (base.Exception != null)
                    return base.Exception;
                return InnerRequest.Exception;
            }
        }

        /// <summary>
        /// Gets the length of the content that will be downloaded.
        /// </summary>
        public long ContentLength
        {
            get
            {
                if (IsChunked)
                    return _chunkHandler.Requests.Sum(x => x.PartialContentLength ?? 0);
                else
                    return ((GetRequest)InnerRequest).ContentLength;
            }
        }

        /// <inheritdoc/>
        public override int AttemptCounter => IsChunked ? _chunkHandler.Requests.Max(x => x.AttemptCounter) : ((GetRequest)InnerRequest).AttemptCounter;

        /// <summary>
        /// Gets a value indicating whether this <see cref="LoadRequest"/> downloads in parts.
        /// </summary>
        public bool IsChunked { get; private set; }

        /// <summary>
        /// Gets the path to the download file.
        /// </summary>
        public string Destination { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the path to the temporary file created during the download process.
        /// </summary>
        public string TempDestination { get; private set; } = string.Empty;

        /// <summary>
        /// Retrieves the progress updates for the download process, enabling real-time monitoring of the download's advancement.
        /// </summary>
        public Progress<float> Progress => ((IProgressableRequest)InnerRequest).Progress;

        /// <summary>
        /// Retrieves the speed reporter for the download process, providing real-time metrics on the download speed. This property is only not null when <see cref="LoadRequestOptions.CreateSpeedReporter"/> is true.
        /// </summary>
        public SpeedReporter<long>? SpeedReporter => ((ISpeedReportable)InnerRequest).SpeedReporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadRequest"/> class.
        /// </summary>
        /// <param name="url">The URL of the file to be downloaded.</param>
        /// <param name="options">The options to customize the load request.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown when the start of the range is greater than or equal to the end of the range.</exception>
        /// <exception cref="NotSupportedException">Thrown when the start of the range is not null and the write mode is set to append.</exception>
        public LoadRequest(string url, LoadRequestOptions? options) : base(url, options)
        {
            if (Range.Start >= Range.End)
                throw new IndexOutOfRangeException(nameof(Range.Start) + " has to be less than " + nameof(Range.End));
            if (Range.Start != null && Options.WriteMode == WriteMode.Append)
                throw new NotSupportedException($"Can not set {nameof(WriteMode.Append)} if {nameof(Range.Start)} is not null");


            if (Options.Chunks > 1)
                IsChunked = true;

            _writeMode = Options.WriteMode;
            CreateDirectory();
            CreateRequest();
        }

        /// <summary>
        /// Creates the directories specified in the options and sets them to the options.
        /// </summary>
        private void CreateDirectory()
        {
            Directory.CreateDirectory(Options.DestinationPath);
            if (!string.IsNullOrWhiteSpace(Options.TempDestination))
                Directory.CreateDirectory(Options.TempDestination);
            else
                Options = Options with { TempDestination = Options.DestinationPath };
        }

        /// <summary>
        /// Creates the <see cref="GetRequest"/> based on the options provided.
        /// </summary>
        private void CreateRequest()
        {
            GetRequestOptions options = Options.ToGetRequestOptions() with { InfosFetched = OnInfosFetched };

            if (IsChunked)
            {
                InnerRequest = _chunkHandler.RequestContainer;
                InnerRequest.StateChanged += OnStateChanged;
                _chunkHandler.RequestContainer.SpeedReporter.Timeout = Options.SpeedReporterTimeout;
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
                SpeedReporter = Options.CreateSpeedReporter ? new() { Timeout = Options.SpeedReporterTimeout } : null,
                RequestStarted = Options.RequestStarted,
                SubsequentRequest = Options.SubsequentRequest,
                InterceptCompletionAsync = OnCompletionAsync
            };

            InnerRequest = new GetRequest(Url, options);
            InnerRequest.StateChanged += OnStateChanged;
            AutoStart();
        }

        /// <summary>
        /// Creates a chunk of the <see cref="GetRequest"/> with the specified index and options.
        /// </summary>
        /// <param name="index">The zero-based index of the chunk to be created.</param>
        /// <param name="options">The options for the get request, including range, filename, and event handlers.</param>
        /// <returns>A new instance of the <see cref="GetRequest"/> class representing the created chunk.</returns>
        private GetRequest CreateChunk(int index, GetRequestOptions options)
        {
            options = options with
            {
                Range = new LoadRange(new Index(index), Options.Chunks),
                SpeedReporter = Options.CreateSpeedReporter ? new() { Timeout = Options.SpeedReporterTimeout } : null,
                Filename = $"{(string.IsNullOrWhiteSpace(Options.Filename) ? "*.*" : Options.Filename)}.{1 + index}_chunk",
                RequestFailed = OnFailure,
                InterceptCompletionAsync = OnCompletionAsync
            };
            if (index == 0)
                options.RequestStarted += Options.RequestStarted;

            return new GetRequest(Url, options);
        }

        /// <summary>
        /// Handles the state change of the request.
        /// </summary>
        /// <param name="sender">The object that triggered the state change event.</param>
        /// <param name="state">The new state of the request, represented by the <see cref="RequestState"/> enumeration.</param>
        private void OnStateChanged(object? sender, RequestState state)
        {
            if (IsChunked && InnerRequest.State == RequestState.Compleated)
                State = RequestState.Running;
            else if (State == RequestState.Idle)
                State = InnerRequest.State;
        }

        /// <summary>
        /// Handles the failure of the request.
        /// </summary>
        /// <param name="request">The failed <see cref="IRequest"/> instance.</param>
        /// <param name="element">The HTTP response message of the failed request, represented by the <see cref="HttpResponseMessage"/> class.</param>
        private void OnFailure(IRequest? request, HttpResponseMessage? element)
        {
            State = RequestState.Failed;
            Cancel();
            _ = ClearOnFailure();
            SynchronizationContext.Post((object? o) => Options.RequestFailed?.Invoke((IRequest)o!, element), this);
        }

        /// <summary>
        /// Handles the fetching of information from the <see cref="GetRequest"/>.
        /// </summary>
        /// <param name="request">The <see cref="GetRequest"/> instance from which information is fetched.</param>
        private void OnInfosFetched(GetRequest? request)
        {
            if (request == null || Filename != null)
                return;
            if (Interlocked.CompareExchange(ref _check, 1, 0) == 1)
                return;
            Filename = request.ContentName;
            try
            {
                if (IsChunked)
                {
                    _chunkHandler.SetInfos(request);
                    CreatePlaceholderFiles(request);
                }
                else
                {
                    CreatePlaceholderFile(request);
                    if (State != RequestState.Running)
                        return;
                }

                SynchronizationContext.Post((o) => Options.InfosFetched?.Invoke((LoadRequest)o!), this);
                ExcludedExtensions(request.ContentExtension);
            }
            catch (Exception ex)
            {
                AddException(ex);
                OnFailure(this, null);
            }

            _check = 0;
        }

        /// <summary>
        /// Creates a file for the download destination.
        /// </summary>
        /// <param name="request">The <see cref="GetRequest"/> instance from which information is fetched.</param>
        /// <remarks>
        /// This method checks if a file exists and loads its information. It also handles file creation based on the write mode.
        /// </remarks>
        private void CreatePlaceholderFile(GetRequest request)
        {
            Destination = Path.Combine(Options.DestinationPath, Filename!);
            TempDestination = request.FilePath;
            switch (_writeMode)
            {
                case WriteMode.CreateNew:
                    string fileExt = Path.GetExtension(Filename!);
                    string contentName = Path.GetFileNameWithoutExtension(Filename!);
                    for (int i = 1; File.Exists(Destination); i++)
                    {
                        Filename = contentName + $"({i})" + fileExt;
                        Destination = Path.Combine(Options.DestinationPath, Filename);
                    }
                    IOManager.Create(Destination);
                    _writeMode = WriteMode.AppendOrTruncate;
                    break;
                case WriteMode.Overwrite:
                    IOManager.Create(Destination);
                    _writeMode = WriteMode.AppendOrTruncate;
                    break;
                case WriteMode.AppendOrTruncate:
                    if (!File.Exists(Destination) || new FileInfo(Destination).Length <= 0)
                        break;
                    IOManager.Create(Destination);
                    _writeMode = WriteMode.AppendOrTruncate;
                    break;
                case WriteMode.Append:
                    if (!File.Exists(Destination) || new FileInfo(Destination).Length <= 0)
                        break;
                    AddException(new FileLoadException($"The file {Filename} at {Destination} already exists. Please change the WriteMode to Create or increase the MinReloadSize."));
                    OnFailure(this, null);
                    break;
            }
        }

        /// <summary>
        /// Checks the part file and loads its information if it exists.
        /// </summary>
        /// <param name="request">The <see cref="GetRequest"/> instance from which information is fetched.</param>
        /// <remarks>
        /// This method checks if a part file exists and loads its information. It also handles file creation based on the write mode.
        /// </remarks>
        private async void CreatePlaceholderFiles(GetRequest request)
        {
            Destination = Path.Combine(Options.DestinationPath, Filename!);
            TempDestination = Path.Combine(Options.TempDestination, Filename + ".part");
            switch (_writeMode)
            {
                case WriteMode.CreateNew:
                    string fileExt = Path.GetExtension(Filename!);
                    string contentName = Path.GetFileNameWithoutExtension(Filename!);
                    for (int i = 1; (File.Exists(TempDestination) && IsChunked) || File.Exists(Destination); i++)
                    {
                        Filename = contentName + $"({i})" + fileExt;
                        Destination = Path.Combine(Options.DestinationPath, Filename!);
                        TempDestination = Path.Combine(Options.TempDestination, Filename + ".part");
                    }
                    IOManager.Create(Destination);
                    IOManager.Create(TempDestination);
                    break;
                case WriteMode.Overwrite:
                    IOManager.Create(Destination);
                    IOManager.Create(TempDestination);
                    break;
                case WriteMode.Append:
                    if (File.Exists(Destination) && new FileInfo(Destination).Length > 0)
                    {
                        AddException(new FileLoadException($"The file '{Filename}' already exists at '{Destination}'. Resolve by changing WriteMode, deleting the file, or avoiding chunked requests."));
                        OnFailure(this, null);
                        break;
                    }
                    if (!File.Exists(TempDestination))
                        break;
                    long appendByteLength = new FileInfo(TempDestination).Length;
                    if (appendByteLength > (request.FullContentLength ?? (request.PartialContentLength * Options.Chunks)))
                    {
                        AddException(new FileLoadException($"The file specified in {TempDestination} has exceeded the expected filesize of the actual downloaded file. Please adjust the WriteMode."));
                        OnFailure(this, null);
                        break;
                    }
                    await _chunkHandler.TrySetBytesAsync(appendByteLength);
                    break;
                case WriteMode.AppendOrTruncate:
                    IOManager.Create(Destination);
                    if (!File.Exists(TempDestination))
                        break;
                    long byteLength = new FileInfo(TempDestination).Length;
                    if (byteLength > (request.FullContentLength ?? (request.PartialContentLength * Options.Chunks)))
                        IOManager.Create(TempDestination);
                    else
                        await _chunkHandler.TrySetBytesAsync(byteLength);
                    break;

            }
        }

        /// <summary>
        /// Checks if the content extension is excluded.
        /// </summary>
        /// <param name="contentExtension">The extension of the content to check.</param>
        /// <remarks>
        /// This method checks if the provided content extension is in the list of excluded extensions. If it is, an exception is added and the failure handler is invoked.
        /// </remarks>
        private void ExcludedExtensions(string contentExtension)
        {
            if (Options.ExcludedExtensions != null && !string.IsNullOrWhiteSpace(contentExtension) && Options.ExcludedExtensions.Any(contentExtension.EndsWith))
            {
                AddException(new InvalidOperationException($"The content extension '{contentExtension}' is invalid."));
                OnFailure(this, null);
            }
        }

        /// <summary>
        /// Handles the completion of the request.
        /// </summary>
        /// <param name="request">The <see cref="IRequest"/> instance that has completed.</param>
        /// <remarks>
        /// This method handles the completion of the request. If the request is chunked, it starts merging the chunks. If the request is not chunked, it moves the temporary file to the destination.
        /// </remarks>
        private async Task OnCompletionAsync(IRequest? request)
        {
            if (request == null)
                return;
            bool canMove = false;
            if (IsChunked)
            {
                _chunkHandler.MarkChunkAsCompleted((GetRequest)request);
                if (Options.MergeWhileProgress || _chunkHandler.AllCompleted)
                    canMove = await _chunkHandler.StartMergeTo(TempDestination);
            }
            else
                canMove = true;

            if (canMove)
            {
                if (Options.SubsequentRequest != null)
                    ((GetRequest)request).TrySetSubsequentRequest(Options.SubsequentRequest);
                TempToDestination();
            }
        }

        /// <summary>
        /// Transfers the temporary file to the final destination.
        /// </summary>
        private void TempToDestination()
        {
            if (Interlocked.CompareExchange(ref _check, 1, 0) == 1)
                return;
            IOManager.Move(TempDestination, Destination);
            State = RequestState.Compleated;
            ((IProgress<float>)Progress).Report(1f);
            SynchronizationContext.Post((o) => Options.RequestCompleated?.Invoke((IRequest)o!, Destination), this);
        }

        /// <summary>
        /// Resets the failure state of the request and deletes temporary files if necessary.
        /// </summary>
        private async Task ClearOnFailure()
        {
            try
            {
                if (!Options.DeleteFilesOnFailure || _isCleared)
                    return;
                _isCleared = true;
                if (File.Exists(TempDestination))
                    File.Delete(TempDestination);
                if (File.Exists(Destination) && new FileInfo(Destination).Length == 0)
                    File.Delete(Destination);
                await _chunkHandler.DeleteChunkFiles(_chunkHandler.RequestContainer.Count);
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }

        /// <summary>
        /// Asynchronously starts the request.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        async Task IRequest.StartRequestAsync() => await RunRequestAsync();

        /// <summary>
        /// Executes the request asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Request{TOptions, TCompleated, TFailed}.RequestReturn"/> object.</returns>
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
        /// Starts the <see cref="LoadRequest"/> if it hasn't started or is paused.
        /// </summary>
        public override void Start()
        {
            State = RequestState.Idle;
            InnerRequest.Start();
        }

        /// <summary>
        /// Pauses the <see cref="LoadRequest"/>.
        /// </summary>
        public override void Pause()
        {
            base.Pause();
            InnerRequest.Pause();
        }

        /// <summary>
        /// Cancels the <see cref="LoadRequest"/>.
        /// </summary>
        public override void Cancel()
        {
            base.Cancel();
            InnerRequest.Cancel();
        }

        /// <summary>
        /// Blocks the current thread until the <see cref="LoadRequest"/> completes execution.
        /// </summary>
        public override void Wait() => Task.Wait();

        /// <summary>
        /// Releases all resources used by the <see cref="LoadRequest"/>.
        /// </summary>
        public override void Dispose()
        {
            InnerRequest.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
