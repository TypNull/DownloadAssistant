using DownloadAssistant.Base;
using DownloadAssistant.Media;
using DownloadAssistant.Options;
using DownloadAssistant.Utilities;
using Requests;
using Requests.Options;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace DownloadAssistant.Request
{
    /// <summary>
    /// Starts <see cref="HttpGet"/> and saves the content stream to a file.
    /// </summary>
    public class GetRequest : WebRequest<GetRequestOptions, string>, IProgressableRequest
    {
        /// <summary>
        /// Min Byte legth to restart the request and download only partial
        /// </summary>
        private const int MIN_RELOAD = 1048576 * 2; //2Mb

        /// <summary>
        /// Bytes that were written to the destination file
        /// </summary>
        public long BytesWritten { get; private set; } = -1;

        /// <summary>
        /// Length of the content that will be downloaded
        /// </summary>
        public long ContentLength => PartialContentLength ?? FullContentLength ?? 0;

        /// <summary>
        /// Gets the full length of the Content also if the <see cref="GetRequest"/> is partial.
        /// </summary>
        public long? FullContentLength => _httpGet.FullContentLength;

        /// <summary>
        /// Gets the partial length of the Content if the <see cref="GetRequest"/> is partial.
        /// </summary>
        public long? PartialContentLength { get; private set; }

        /// <summary>
        /// Name of the file that should be downloaded.
        /// </summary>
        public string Filename { get; private set; }

        /// <summary>
        /// Name of the content from the server
        /// </summary>
        public string ContentName { get; private set; } = string.Empty;
        /// <summary>
        /// Extension of the content from the server
        /// </summary>
        public string ContentExtension { get; private set; } = string.Empty;

        /// <summary>
        /// Content Headers of the last attempt.
        /// </summary>
        public HttpContentHeaders? ContentHeaders { get; private set; }

        /// <summary>
        /// Range that should be downloaded
        /// </summary>
        private LoadRange Range => Options.Range;

        /// <summary>
        /// Path to the download file
        /// </summary>
        public string FilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Progress to get updates of the download process.
        /// </summary>
        public Progress<float> Progress => (Progress<float>)_progress;
        private readonly IProgress<float> _progress;


        private HttpGet _httpGet = null!;
        private WriteMode _mode = WriteMode.Append;


        /// <summary>
        /// Creates a GetRequest
        /// </summary>
        /// <param name="url">url to load</param>
        /// <param name="options">otions to change this request</param>
        /// <exception cref="NotSupportedException">Can not set LoadMode.Append and Range.Start</exception>
        public GetRequest(string url, GetRequestOptions? options = null) : base(url, options)
        {
            Directory.CreateDirectory(Options.DirectoryPath);
            Filename = Options.Filename.Trim();
            _mode = Options.WriteMode;
            _progress = Options.Progress ?? new();
            LoadWrittenBytes();
            SetHttpGet();
            AutoStart();
        }

        /// <summary>
        /// Loads file info if the file exsists
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void LoadWrittenBytes()
        {
            if (_mode != WriteMode.Append || BytesWritten > 0 || Filename == string.Empty || !Filename.Contains('.'))
                return;

            FilePath = Path.Combine(Options.DirectoryPath, Filename);
            if (File.Exists(FilePath))
            {
                BytesWritten = new FileInfo(FilePath).Length;
                Debug.WriteLine("Set BytesWritten to " + BytesWritten);
            }
        }

        /// <summary>
        /// Creates a HttpGet object that holds the http informations
        /// </summary>
        private void SetHttpGet()
        => _httpGet = new(GetPresetRequestMessage(new(HttpMethod.Get, Url)), Options.SupportsHeadRequest)
        {
            Token = Token,
            Range = Range,
            Timeout = Options.Timeout,
            SecondRange = new LoadRange(Options.MinByte, Options.MaxByte)
        };

        /// <summary>
        /// Handels the request of this <see cref="IRequest"/>.
        /// </summary>
        /// <returns>A RequestReturn object</returns>
        protected override async Task<RequestReturn> RunRequestAsync()
        {
            RequestReturn result = new();
            try
            {
                result = await LoadAsync();
                result.FailedReturn?.Dispose();
                if (State == RequestState.Running && result.Successful)
                    _progress.Report(1f);
            }
            catch (Exception ex)
            {
                AddException(ex);
                Debug.Assert(true, ex.Message);
            }
            return result;
        }


        private async Task<RequestReturn> LoadAsync()
        {
            if (IsFinished())
                return new(true, FilePath, null);

            HttpResponseMessage res = await SendRequestAsync();
            if (!res.IsSuccessStatusCode || State != RequestState.Running)
                return new(false, FilePath, res);
            bool noBytesWritten = BytesWritten <= 0;

            if (noBytesWritten)
                SetFileInfo(res);

            if (IsFinished())
                return new(true, FilePath, null);

            res = await ReloadActions(res, noBytesWritten);

            await WriteToFileAsync(res);

            return new(true, FilePath, res);
        }


        /// <summary>
        /// Sets the ContentLength of the request if known
        /// </summary>
        /// <param name="value">The length</param>
        /// <exception cref="ArgumentOutOfRangeException">The length can not be 0 or less</exception>
        public void SetContentLength(long value)
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (FullContentLength == null)
                _httpGet.SetContentLength(value);
        }


        /// <summary>
        /// Checks if further actions on the file or the request are nessesary
        /// </summary>
        /// <param name="res"></param>
        /// <param name="noBytesWritten"></param>
        /// <returns>The original or updated Response</returns>
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
        /// Sets the Informationt to the httpRequest
        /// </summary>
        /// <param name="res"></param>
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
                    _mode = WriteMode.Append;
                    break;
                case WriteMode.Create:
                    IOManager.Create(FilePath);
                    _mode = WriteMode.Append;
                    break;
                case WriteMode.Append:
                    LoadWrittenBytes();
                    if (BytesWritten > ContentLength)
                    {
                        IOManager.Create(FilePath);
                        BytesWritten = 0;
                    }
                    break;
            }

            Options.InfosFetched?.Invoke(this);
        }

        /// <summary>
        /// Starts the httpGet Request and overwrites the bytesWritten
        /// </summary>
        /// <returns></returns>
        private async Task<HttpResponseMessage> SendRequestAsync()
        {
            _httpGet.AddBytesToStart(BytesWritten < 1 ? 0 : BytesWritten);
            return await _httpGet.LoadResponseAsync();
        }

        /// <summary>
        /// Writes the response to a file.
        /// </summary>
        /// <param name="res">Response of <see cref="HttpClient"/></param>
        /// <returns>A awaitable Task</returns>
        private async Task WriteToFileAsync(HttpResponseMessage res)
        {
            using ThrottledStream? responseStream = new(await res.Content.ReadAsStreamAsync(Token));
            responseStream.MaximumBytesPerSecond = Options.MaxBytesPerSecond ?? 0;

            using FileStream? fileStream = new(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await WriterAsync(responseStream, fileStream);
            await fileStream.FlushAsync();
        }

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
        /// Indicates if the file was downloaded before.
        /// </summary>
        /// <returns>A bool to indicate if <see cref="BytesWritten"/> is equal to <see cref="ContentLength"/></returns>
        private bool IsFinished() => BytesWritten == ContentLength;

        /// <summary>
        /// If the request is partial
        /// </summary>
        /// <returns>bool</returns>
        public bool IsPartial() => _httpGet.IsPartial();

        /// <summary>
        /// If the last request should be partial
        /// </summary>
        /// <returns>bool</returns>
        public bool ShouldBePartial() => _httpGet.HasToBePartial();

        /// <summary>
        /// Checks if the HttpRequest should be send again. When the Filename exists on drive
        /// </summary>
        /// <param name="noBytesWritten">if the request didn't know before that the file on drive exists</param>
        /// <returns>A bool that indicates if a reload is nessesary</returns>
        private bool CheckReload(bool noBytesWritten) => noBytesWritten && BytesWritten >= MIN_RELOAD;

        /// <summary>
        /// Checks if the HttpRequest Should be partioal but is not
        /// </summary>
        /// <param name="noBytesWritten">if the request didn't know before that the file on drive exists</param>
        /// <returns>A bool that indicates if the file should be deleted</returns>
        private bool CheckClearFile(bool noBytesWritten) => ((!IsPartial()) && ShouldBePartial() && BytesWritten > 0) || (noBytesWritten && BytesWritten < MIN_RELOAD);


        /// <summary>
        /// Dispose the <see cref="GetRequest"/>. 
        /// Will be called automaticly by the <see cref="RequestHandler"/>.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public override void Dispose()
        {
            base.Dispose();
            _httpGet.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

