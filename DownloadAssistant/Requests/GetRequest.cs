using DownloadAssistant.Base;
using DownloadAssistant.Media;
using DownloadAssistant.Options;
using DownloadAssistant.Utilities;
using Requests;
using Requests.Options;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace DownloadAssistant.Requests
{
    /// <summary>
    /// Represents a GET request to download content from a specified URL, supporting progress tracking and speed reporting.
    /// </summary>
    /// <remarks>
    /// This class inherits from <see cref="WebRequest{TOptions, TResult}"/> and implements <see cref="IProgressableRequest"/> and <see cref="ISpeedReportable"/>.
    /// It initiates an <see cref="HttpGet"/> operation and manages the content stream, saving it to a file while providing progress updates and speed metrics.
    /// </remarks>
    public class GetRequest : WebRequest<GetRequestOptions, string>, IProgressableRequest, ISpeedReportable
    {
        /// <summary>
        /// Gets the number of bytes that were written to the destination file.
        /// </summary>
        public long BytesWritten { get; private set; } = -1;

        /// <summary>
        /// Gets the length of the content that will be downloaded.
        /// </summary>
        /// <remarks>
        /// This property returns the partial content length if the <see cref="GetRequest"/> is partial, 
        /// otherwise it returns the full content length.
        /// </remarks>
        public long ContentLength => PartialContentLength ?? FullContentLength ?? 0;

        /// <summary>
        /// Gets the full length of the content, even if the <see cref="GetRequest"/> is partial.
        /// </summary>
        public long? FullContentLength => _httpGet.FullContentLength;

        /// <summary>
        /// Gets the partial length of the content if the <see cref="GetRequest"/> is partial.
        /// </summary>
        public long? PartialContentLength { get; private set; }

        /// <summary>
        /// Gets the current transfer rate in bytes per second.
        /// </summary>
        public long CurrentBytesPerSecond => _responseStream?.CurrentBytesPerSecond ?? 0;

        /// <summary>
        /// Gets the name of the file that should be downloaded.
        /// </summary>
        public string Filename { get; private set; }

        /// <summary>
        /// Gets the name of the content from the server.
        /// </summary>
        public string ContentName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the extension of the content from the server.
        /// </summary>
        public string ContentExtension { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the content headers of the last attempt.
        /// </summary>
        public HttpContentHeaders? ContentHeaders { get; private set; }

        /// <summary>
        /// Gets the range that should be downloaded.
        /// </summary>
        private LoadRange Range => Options.Range;

        /// <summary>
        /// Gets the path to the download file.
        /// </summary>
        public string FilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Provides access to the progress updates of the download process, allowing monitoring of the download's advancement.
        /// </summary>
        public Progress<float> Progress => (Progress<float>)_progress;
        private readonly IProgress<float> _progress;

        /// <summary>
        /// Provides access to the speed reporter, which offers real-time metrics on the download speed.
        /// </summary>
        public SpeedReporter<long>? SpeedReporter => (SpeedReporter<long>?)_speedReporter;
        private readonly IProgress<long>? _speedReporter;

        private HttpGet _httpGet = null!;
        private ThrottledStream? _responseStream;
        private WriteMode _mode;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetRequest"/> class.
        /// </summary>
        /// <param name="url">The URL to load.</param>
        /// <param name="options">The options to change this request.</param>
        /// <exception cref="NotSupportedException">Thrown when LoadMode.Append and Range.Start are set.</exception>
        public GetRequest(string url, GetRequestOptions? options = null) : base(url, options)
        {
            Directory.CreateDirectory(Options.DirectoryPath);
            Filename = Options.Filename.Trim();
            _mode = Options.WriteMode;
            _progress = Options.Progress ?? new();
            _speedReporter = Options.SpeedReporter;
            LoadWrittenBytes();
            SetHttpGet();
            AutoStart();
        }

        /// <summary>
        /// Loads file info if the file exists.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the mode is not WriteMode.Append, BytesWritten is greater than 0, Filename is empty, or Filename does not contain '.'.</exception>
        private void LoadWrittenBytes()
        {
            if ((_mode != WriteMode.Append && _mode != WriteMode.AppendOrTruncate) || BytesWritten > 0 || Filename == string.Empty || !Filename.Contains('.'))
                return;

            FilePath = Path.Combine(Options.DirectoryPath, Filename);
            if (File.Exists(FilePath))
                BytesWritten = new FileInfo(FilePath).Length;
            BytesWritten = BytesWritten == 0 ? -1 : BytesWritten;
        }

        /// <summary>
        /// Allows the request to complete successfully without performing any additional actions.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task RunToCompleatedAsync()
        {
            RequestState state = State;
            State = RequestState.Compleated;
            if (state != RequestState.Running)
                SetTaskState();

            if (State == RequestState.Compleated)
            {
                await Task;
                _progress.Report(1f);
                SynchronizationContext.Post((o) => Options.RequestCompleated?.Invoke((GetRequest)o!, FilePath), this);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpGet"/> class that holds the HTTP information.
        /// </summary>
        private void SetHttpGet() => _httpGet = new(GetPresetRequestMessage(new(HttpMethod.Get, Url)), Options.SupportsHeadRequest)
        {
            Token = Token,
            Range = Range,
            Timeout = Options.Timeout,
            SecondRange = new LoadRange(Options.MinByte, Options.MaxByte)
        };

        /// <summary>
        /// Handles the request of this <see cref="IRequest"/>.
        /// </summary>
        /// <returns>A <see cref="Request{TOptions, TCompleated, TFailed}.RequestReturn"/> object that represents the result of the request.</returns>
        protected override async Task<RequestReturn> RunRequestAsync()
        {
            RequestReturn result = new();
            try
            {
                result = await LoadAsync();
                result.FailedReturn?.Dispose();
                if (State == RequestState.Running && result.Successful)
                {
                    _progress.Report(1f);
                    await (Options.InterceptCompletionAsync?.Invoke(this) ?? Task.CompletedTask);
                }
            }
            catch (Exception ex)
            {
                AddException(ex);
                Debug.Assert(false, ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Asynchronously loads the request.
        /// </summary>
        /// <returns>A <see cref="Request{TOptions, TCompleated, TFailed}.RequestReturn"/> object that represents the result of the load operation.</returns>
        private async Task<RequestReturn> LoadAsync()
        {
            if (IsFinished())
                return new(true, FilePath, null);

            HttpResponseMessage res = await SendRequestAsync();
            if (!res.IsSuccessStatusCode || State != RequestState.Running)
                return new(false, FilePath, res);
            bool noBytesWritten = BytesWritten <= 0;

            if (string.IsNullOrEmpty(ContentName))
                SetFileInfo(res);

            if (IsFinished())
                return new(true, FilePath, null);

            res = await ReloadActions(res, noBytesWritten);

            await WriteToFileAsync(res);

            return new(true, FilePath, res);
        }


        /// <summary>
        /// Sets the content length of the request if known.
        /// </summary>
        /// <param name="value">The length of the content.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is 0 or less.</exception>
        public void SetContentLength(long value)
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (FullContentLength == null)
                _httpGet.SetContentLength(value);
        }


        /// <summary>
        /// Checks if further actions on the file or the request are necessary.
        /// </summary>
        /// <param name="res">The HTTP response message.</param>
        /// <param name="noBytesWritten">A boolean value that indicates whether no bytes were written.</param>
        /// <returns>The original or updated <see cref="HttpResponseMessage"/>.</returns>
        private async Task<HttpResponseMessage> ReloadActions(HttpResponseMessage res, bool noBytesWritten)
        {
            if (CheckReload(noBytesWritten))
            {
                res = await SendRequestAsync();
                ContentHeaders = res.Content.Headers;
            }

            if (CheckClearFile(noBytesWritten))
            {
                IOManager.Create(FilePath);
                BytesWritten = 0;
                PartialContentLength = null;
            }

            return res;
        }

        /// <summary>
        /// Sets the information for the HTTP request.
        /// </summary>
        /// <param name="res">The HTTP response message.</param>
        private void SetFileInfo(HttpResponseMessage res)
        {
            FileMetadata fileData = new(res.Content.Headers, _uri);
            Filename = fileData.BuildFilename(Options.Filename);
            FilePath = Path.Combine(Options.DirectoryPath, Filename);
            ContentName = Path.GetFileNameWithoutExtension(Filename);
            ContentExtension = fileData.Extension;
            ContentHeaders = res.Content.Headers;
            PartialContentLength = _httpGet.PartialContentLength;

            switch (_mode)
            {
                case WriteMode.CreateNew:
                    string fileExt = Path.GetExtension(Filename);
                    for (int i = 1; File.Exists(FilePath); i++)
                    {
                        Filename = ContentName + $"({i})" + fileExt;
                        FilePath = Path.Combine(Options.DirectoryPath, Filename);
                    }
                    IOManager.Create(FilePath);
                    _mode = WriteMode.AppendOrTruncate;
                    break;
                case WriteMode.Overwrite:
                    IOManager.Create(FilePath);
                    _mode = WriteMode.AppendOrTruncate;
                    break;
                case WriteMode.AppendOrTruncate:
                    LoadWrittenBytes();
                    if (BytesWritten <= ContentLength)
                        break;
                    IOManager.Create(FilePath);
                    BytesWritten = 0;
                    break;
                case WriteMode.Append:
                    LoadWrittenBytes();
                    if (BytesWritten > ContentLength)
                    {
                        AddException(new FileLoadException($"The file specified in {FilePath} has exceeded the expected filesize of the actual downloaded file. Please adjust the WriteMode."));
                        State = RequestState.Failed;
                        SynchronizationContext.Post((object? o) => Options.RequestFailed?.Invoke((IRequest)o!, null), this);
                    }
                    break;
            }
            Options.InfosFetched?.Invoke(this);
        }

        /// <summary>
        /// Starts the HTTP GET request and overwrites the bytes written.
        /// </summary>
        /// <returns>The HTTP response message.</returns>
        private async Task<HttpResponseMessage> SendRequestAsync()
        {
            _httpGet.AddBytesToStart(BytesWritten < 1 ? 0 : BytesWritten);
            return await _httpGet.LoadResponseAsync();
        }

        /// <summary>
        /// Writes the response to a file.
        /// </summary>
        /// <param name="res">The HTTP response message.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        private async Task WriteToFileAsync(HttpResponseMessage res)
        {
            using ThrottledStream? responseStream = new(await res.Content.ReadAsStreamAsync(Token))
            {
                MaximumBytesPerSecond = Options.MaxBytesPerSecond ?? 0,
                SpeedReporter = SpeedReporter,
            };
            _responseStream = responseStream;

            using FileStream? fileStream = new(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await WriterAsync(responseStream, fileStream);
            await fileStream.FlushAsync();
            _responseStream = null;
        }

        /// <summary>
        /// Writes the response stream to the file stream.
        /// </summary>
        /// <param name="responseStream">The throttled response stream.</param>
        /// <param name="fileStream">The file stream.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        private async Task WriterAsync(ThrottledStream responseStream, FileStream fileStream)
        {
            while (State == RequestState.Running)
            {
                byte[]? buffer = new byte[Options.BufferLength];
                int bytesRead = await responseStream.ReadAsync(buffer, Token).ConfigureAwait(false);

                if (bytesRead == 0) break;
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), Token);
                BytesWritten += bytesRead;
                _progress.Report((float)BytesWritten / (ContentLength + 100));
                Debug.Assert((float)BytesWritten / (ContentLength + 100) < 1f);
            }
        }


        /// <summary>
        /// Determines whether the file download is finished.
        /// </summary>
        /// <returns><c>true</c> if the number of bytes written is equal to the content length; otherwise, <c>false</c>.</returns>
        private bool IsFinished() => BytesWritten == ContentLength;

        /// <summary>
        /// Determines whether the request is partial.
        /// </summary>
        /// <returns><c>true</c> if the HTTP GET request is partial; otherwise, <c>false</c>.</returns>
        public bool IsPartial() => _httpGet.IsPartial();

        /// <summary>
        /// Determines whether the last request should be partial.
        /// </summary>
        /// <returns><c>true</c> if the HTTP GET request has to be partial; otherwise, <c>false</c>.</returns>
        public bool ShouldBePartial() => _httpGet.HasToBePartial();

        /// <summary>
        /// Checks if the HTTP request should be sent again when the filename exists on the drive.
        /// </summary>
        /// <param name="noBytesWritten">A boolean value that indicates whether the request didn't know before that the file on the drive exists.</param>
        /// <returns><c>true</c> if a reload is necessary; otherwise, <c>false</c>.</returns>
        private bool CheckReload(bool noBytesWritten) => noBytesWritten && BytesWritten >= Options.MinReloadSize;

        /// <summary>
        /// Checks if the HTTP request should be partial but is not.
        /// </summary>
        /// <param name="noBytesWritten">A boolean value that indicates whether the request didn't know before that the file on the drive exists.</param>
        /// <returns><c>true</c> if the file should be deleted; otherwise, <c>false</c>.</returns>
        private bool CheckClearFile(bool noBytesWritten) => ((!IsPartial()) && ShouldBePartial() && BytesWritten > 0) || (noBytesWritten && BytesWritten < Options.MinReloadSize);

        /// <summary>
        /// Disposes the <see cref="GetRequest"/> instance.
        /// </summary>
        /// <remarks>
        /// This method is called automatically by the <see cref="RequestHandler"/>.
        /// </remarks>
        /// <exception cref="AggregateException">Thrown when one or more errors occur during the disposal of the <see cref="GetRequest"/> instance.</exception>
        /// <exception cref="ArgumentException">Thrown when an argument provided to a method is not valid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when an attempt is made to access an object that has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a method call is invalid for the object's current state.</exception>
        public override void Dispose()
        {
            base.Dispose();
            _httpGet.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

