using DownloadAssistant.Base;
using DownloadAssistant.Media;
using DownloadAssistant.Options;

namespace DownloadAssistant.Requests
{
    /// <summary>
    /// Represents a HEAD request with comprehensive metadata analysis and fallback to GET when HEAD is not supported.
    /// </summary>
    public class StatusRequest : WebRequest<WebRequestOptions<HttpResponseMessage>, HttpResponseMessage>
    {
        private CancellationTokenSource? _timeoutCTS;
        private HttpResponseMessage? _lastResponse;

        /// <summary>
        /// Gets the metadata extracted from the response headers including filename, extension, and content type information.
        /// </summary>
        public FileMetadata? FileMetadata { get; private set; }

        /// <summary>
        /// Gets the media type classification of the requested resource.
        /// </summary>
        public WebType? FileType { get; private set; }

        /// <summary>
        /// Gets the content length reported by the server, if available.
        /// </summary>
        /// <remarks>
        /// This value might not be reliable for chunked or compressed content.
        /// Check <see cref="HasReliableContentLength"/> for validity.
        /// </remarks>
        public long? ContentLength { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="ContentLength"/> value can be considered reliable.
        /// </summary>
        /// <value>
        /// <c>true</c> if the content length is not chunked, not compressed, and obtained via HEAD request; otherwise, <c>false</c>.
        /// </value>
        public bool HasReliableContentLength { get; private set; }

        /// <summary>
        /// Gets the server software information from the response headers.
        /// </summary>
        public string? Server { get; private set; }

        /// <summary>
        /// Gets the last modified timestamp of the resource, if provided by the server.
        /// </summary>
        public DateTimeOffset? LastModified { get; private set; }

        /// <summary>
        /// Gets the entity tag (ETag) for cache validation, if provided by the server.
        /// </summary>
        public string? ETag { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the server supports partial content requests.
        /// </summary>
        public bool SupportsRangeRequests { get; private set; }

        /// <summary>
        /// Gets the content encoding type used for the response body.
        /// </summary>
        public string? ContentEncoding { get; private set; }

        /// <summary>
        /// Gets the natural language(s) of the intended audience for the response content.
        /// </summary>
        public string? ContentLanguage { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the response content is compressed (gzip or deflate).
        /// </summary>
        public bool IsCompressed => ContentEncoding?.Contains("gzip") == true
                                  || ContentEncoding?.Contains("deflate") == true;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusRequest"/> class.
        /// </summary>
        /// <param name="url">The URL to send the HEAD request to.</param>
        /// <param name="options">Configuration options for the request.</param>
        public StatusRequest(string url, WebRequestOptions<HttpResponseMessage>? options = null) : base(url, options) => AutoStart();

        /// <summary>
        /// Executes the HEAD request and processes the response.
        /// </summary>
        /// <returns>
        /// A <see cref="RequestReturn"/> object containing:
        /// - The HTTP response message in both success and failure cases
        /// - Success status flag
        /// - Any exceptions that occurred during processing
        /// </returns>
        protected override async Task<RequestReturn> RunRequestAsync()
        {
            RequestReturn returnObject = new();
            try
            {
                using (_lastResponse = await SendHttpMessage())
                {
                    ParseMetadata(_lastResponse);
                    returnObject = StatusRequest.BuildReturnObject(_lastResponse);
                }
                _timeoutCTS?.Dispose();
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("405"))
            {
                AddException(ex);
                return await HandleHeadFallback();
            }
            catch (Exception ex) { AddException(ex); }
            return returnObject;
        }

        /// <summary>
        /// Handles fallback to GET request when HEAD method is not allowed.
        /// </summary>
        /// <returns>A <see cref="RequestReturn"/> object with the GET response data.</returns>
        private async Task<RequestReturn> HandleHeadFallback()
        {
            HttpGet getRequest = new(new HttpRequestMessage(HttpMethod.Get, _uri), supportHeadRequest: false);
            try
            {
                using HttpResponseMessage response = await getRequest.LoadResponseAsync();
                ParseMetadata(response);
                return StatusRequest.BuildReturnObject(response);
            }
            finally
            {
                getRequest.Dispose();
            }
        }

        /// <summary>
        /// Extracts and processes metadata from the HTTP response.
        /// </summary>
        /// <param name="response">The response to analyze.</param>
        private void ParseMetadata(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode) return;

            // Core file metadata
            FileMetadata = new FileMetadata(response.Content.Headers, _uri);

            // MIME type analysis
            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            FileType = new WebType(mediaType);

            // Server information
            Server = response.Headers.Server?.ToString();
            LastModified = response.Content.Headers.LastModified;
            ETag = response.Headers.ETag?.Tag;

            // Content negotiation
            ContentEncoding = response.Content.Headers.ContentEncoding.ToString();
            ContentLanguage = response.Content.Headers.ContentLanguage.ToString();
            SupportsRangeRequests = response.Headers.AcceptRanges.Any();

            // Content length handling
            ContentLength = response.Content.Headers.ContentLength;
            ValidateContentLength(response);
        }

        /// <summary>
        /// Validates the reliability of the Content-Length header value.
        /// </summary>
        /// <param name="response">The response to validate.</param>
        private void ValidateContentLength(HttpResponseMessage response) =>
            HasReliableContentLength = ContentLength.HasValue &&
            !response.Headers.TransferEncodingChunked.HasValue &&
            !IsCompressed &&
            response.RequestMessage?.Method == HttpMethod.Head;

        /// <summary>
        /// Constructs a standardized return object from the HTTP response.
        /// </summary>
        /// <param name="response">The response to package.</param>
        /// <returns>A configured <see cref="RequestReturn"/> instance.</returns>
        private static RequestReturn BuildReturnObject(HttpResponseMessage response) => new()
        {
            Successful = response.IsSuccessStatusCode,
            CompleatedReturn = response,
            FailedReturn = response
        };

        /// <summary>
        /// Sends the HEAD request message to the server.
        /// </summary>
        /// <returns>The received HTTP response.</returns>
        private async Task<HttpResponseMessage> SendHttpMessage()
        {
            SetTimedToken();
            HttpRequestMessage request = GetPresetRequestMessage(new HttpRequestMessage(HttpMethod.Head, _uri.AbsoluteUri));
            return await HttpGet.HttpClient.SendAsync(request, _timeoutCTS!.Token);
        }

        /// <summary>
        /// Configures the timeout cancellation token for the request.
        /// </summary>
        private void SetTimedToken()
        {
            _timeoutCTS?.Dispose();
            _timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(Token);
            _timeoutCTS.CancelAfter(Options.Timeout ?? TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Determines if the content type is likely downloadable.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the content type is media, application, or archive; otherwise, <c>false</c>.
        /// </returns>
        public bool IsLikelyDownloadable() =>
            FileType?.IsMedia == true ||
            FileType?.IsApplication == true ||
            FileType?.IsArchive == true;

        /// <summary>
        /// Checks if the server supports resumable downloads.
        /// </summary>
        /// <returns>
        /// <c>true</c> if range requests are supported and content length is reliable; otherwise, <c>false</c>.
        /// </returns>
        public bool SupportsResume() =>
            SupportsRangeRequests && HasReliableContentLength;

        /// <summary>
        /// Gets the Age header value indicating how long the response has been cached.
        /// </summary>
        /// <returns>The age duration or null if not specified.</returns>
        public TimeSpan? AgeHeaderValue() =>
            _lastResponse?.Headers.Age;

        /// <summary>
        /// Retrieves alternative content locations from the Content-Location header.
        /// </summary>
        /// <returns>Enumerable of alternative URIs or null if not specified.</returns>
        public IEnumerable<Uri>? ContentLocations() =>
            _lastResponse?.Headers.GetValues("Content-Location")?.Select(v => new Uri(v));

        /// <summary>
        /// Gets combined values from a specified response header.
        /// </summary>
        /// <param name="headerName">The header name to retrieve.</param>
        /// <returns>Comma-separated header values or null if not found.</returns>
        public string? GetHeaderValue(string headerName) =>
            _lastResponse?.Headers.TryGetValues(headerName, out IEnumerable<string>? values) == true
                ? string.Join(", ", values)
                : null;
    }
}