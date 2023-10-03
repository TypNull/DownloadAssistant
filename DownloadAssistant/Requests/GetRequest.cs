using DownloadAssistant.Base;
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
    public class GetRequest : WebRequest<GetRequestOptions, GetRequest>, IProgressable
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
        public long ContentLength => PartialContentLegth ?? FullContentLegth ?? 0;

        /// <summary>
        /// Gets the full length of the Content also if the <see cref="GetRequest"/> is partial.
        /// </summary>
        public long? FullContentLegth => _httpGet.FullContentLength;

        /// <summary>
        /// Gets the partial length of the Content if the <see cref="GetRequest"/> is partial.
        /// </summary>
        public long? PartialContentLegth => _httpGet.PartialContentLength;

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
        public GetRequest(string url, GetRequestOptions? options) : base(url, options)
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
        /// Sets the ContentLength of the request if known
        /// </summary>
        /// <param name="value">The length</param>
        /// <exception cref="ArgumentOutOfRangeException">The length can not be 0 or less</exception>
        public void SetContentLength(long value)
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (FullContentLegth == null)
                _httpGet.SetContentLength(value);
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
                BytesWritten = new FileInfo(FilePath).Length;
        }

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
                return new(true, this, null);

            HttpResponseMessage res = await SendRequestAsync();

            if (!res.IsSuccessStatusCode || State != RequestState.Running)
                return new(false, this, res);

            bool noBytesWritten = BytesWritten <= 0;
            Media.FileMetadata file = new(res.Content.Headers, _uri);
            Filename = file.BuildFilename(Options.Filename);
            ContentName = Path.GetFileNameWithoutExtension(Filename);
            ContentExtension = file.Extension;

            SetFileInfo();

            if (IsFinished())
                return new(true, this, null);

            res = await FileInfosFetched(res, noBytesWritten);
            return new(true, this, res);
        }

        private async Task<HttpResponseMessage> FileInfosFetched(HttpResponseMessage res, bool noBytesWritten)
        {
            if (CheckReload(noBytesWritten))
            {
                _httpGet.AddBytesToStart(BytesWritten + 1);
                res = await SendRequestAsync();
            }

            if (CheckClearFile())
            {
                IOManager.Create(FilePath);
                BytesWritten = 0;
            }

            ContentHeaders = res.Content.Headers;

            _ = Task.Run(() => Options.InfosFetched?.Invoke(this));

            await WriteToFileAsync(res);
            return res;
        }

        private void SetFileInfo()
        {
            FilePath = Path.Combine(Options.DirectoryPath, Filename);
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
        }

        private bool CheckReload(bool noBytesWritten) => noBytesWritten && BytesWritten >= MIN_RELOAD;

        private bool CheckClearFile() => (!IsPartial()) && ShouldBePartial() && BytesWritten > 0;

        private async Task<HttpResponseMessage> SendRequestAsync()
        {
            _httpGet.AddBytesToStart(BytesWritten < 1 ? 0 : BytesWritten + 1);
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

