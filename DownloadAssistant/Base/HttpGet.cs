using System;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace DownloadAssistant.Base
{
    /// <summary>
    /// Class to create an instance of <see cref="HttpGet"/>.
    /// </summary>
    public partial class HttpGet : IDisposable
    {
        /// <summary>
        /// Gets or sets the primary download range of the HTTP GET request.
        /// </summary>
        /// <see cref="LoadRange"/>
        public LoadRange Range
        {
            get => _range; init
            {
                _range = value;
                InitAddToStart();
            }
        }
        private LoadRange _range = new();

        /// <summary>
        /// Sets a secondary range. The values between the primary and secondary ranges will be set as the new primary range.
        /// </summary>
        public LoadRange SecondRange { init { _secondRange = value; InitAddToStart(); } }
        private readonly LoadRange _secondRange = new();
        private long? _addToStart;

        /// <summary>
        /// Gets or sets the timeout for the HTTP GET request.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets the cancellation token for this HTTP GET request.
        /// </summary>
        public CancellationToken Token { get; init; }


        /// <summary>
        /// Gets the exception if the HEAD request failed.
        /// </summary>
        public Exception? HeadRequestException { get; private set; }

        private readonly HttpRequestMessage _originalMessage;
        private HttpResponseMessage? _lastResponse;

        /// <summary>
        /// Gets the full content length of the response.
        /// </summary>
        public long? FullContentLength => _contentLength.IsValueCreated ? _contentLength.Value : null;

        private Lazy<long> _contentLength;
        private bool _disposed;

        /// <summary>
        /// Gets the length of the part of the content that will be received when the range is set.
        /// </summary>
        public long? PartialContentLength { get; private set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="HttpGet"/> class.
        /// </summary>
        /// <param name="msg">The HTTP request message to send.</param>
        /// <param name="supportHeadRequest">Indicates whether the server supports HEAD requests. If not, set this value to false.</param>
        /// <exception cref="NotSupportedException">Thrown when the HTTP method in the request message is not GET.</exception>
        public HttpGet(HttpRequestMessage msg, bool supportHeadRequest = true)
        {
            if (msg.Method != HttpMethod.Get)
                throw new NotSupportedException($"The HttpMethod in HttpRequestMessage can only be set to HttpMethod.Get");
            ArgumentNullException.ThrowIfNull(msg.RequestUri);

            _originalMessage = msg;
            _contentLength = supportHeadRequest ? new(LoadContentLength) : new(0L);
        }

        /// <summary>
        /// Sets the content length for the HTTP GET request.
        /// </summary>
        /// <param name="contentLength">The content length to set.</param>
        public void SetContentLength(long contentLength)
        {
            _contentLength = new(contentLength);
            if (!Range.IsEmpty)
                SetRange(FullContentLength!.Value);
        }

        /// <summary>
        /// Initializes the lazy <see cref="_contentLength"/>. This method is called only once.
        /// </summary>
        /// <returns>A nullable long that represents the length of the content.</returns>
        private long LoadContentLength()
        {
            long length = 0;
            try
            {
                length = GetContentLength();
            }
            catch (Exception ex)
            {
                HeadRequestException = new NotSupportedException("The length of the content could not be loaded, because the requested server does not support this function.", ex);
                Debug.Assert(false, ex.Message);
            }
            if (!Range.IsEmpty && length > 0)
                SetRange(length);
            return length;
        }

        /// <summary>
        /// Retrieves the content length from the server.
        /// </summary>
        /// <returns>The content length.</returns>
        private long GetContentLength()
        {
            HttpRequestMessage msg = CloneRequestMessage(_originalMessage);
            msg.Method = HttpMethod.Head;
            HttpResponseMessage res = HttpClient.Send(msg, TimedTokenOrDefault());
            if (res.IsSuccessStatusCode)
                return res.Content.Headers.ContentLength ?? 0;
            return 0;
        }

        /// <summary>
        /// Creates an instance of <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <returns>The response message.</returns>
        public async Task<HttpResponseMessage> LoadResponseAsync()
        {
            if (!IsLengthSet(out HttpResponseMessage res))
                return res;

            HttpRequestMessage msg = CloneRequestMessage(_originalMessage);
            if (HasToBePartial())
                msg.Headers.Range = new RangeHeaderValue((Range.Start ?? 0) + (_addToStart ?? 0), Range.End);

            return await SendHttpMenssage(msg);
        }

        /// <summary>
        /// Checks if the content length is set.
        /// </summary>
        /// <param name="res">The response message.</param>
        /// <returns>True if the content length is set; otherwise, false.</returns>
        private bool IsLengthSet(out HttpResponseMessage res)
        {
            if (HasToBePartial() && _contentLength.Value == 0)
            {
                res = HttpClient.Send(CloneRequestMessage(_originalMessage), HttpCompletionOption.ResponseHeadersRead, TimedTokenOrDefault());
                if (res.IsSuccessStatusCode)
                {
                    _contentLength = new(res.Content.Headers.ContentLength ?? 0);
                    if (FullContentLength == 0)
                        return false;
                    if (!Range.IsEmpty)
                        SetRange(FullContentLength!.Value);
                }
                else
                    return false;
            }
            res = new();
            return true;
        }

        /// <summary>
        /// Sends an HTTP request message.
        /// </summary>
        /// <param name="msg">The HTTP request message to send.</param>
        /// <returns>The response message.</returns>
        private async Task<HttpResponseMessage> SendHttpMenssage(HttpRequestMessage msg)
        {
            _lastResponse?.Dispose();
            CancellationToken token = TimedTokenOrDefault();
            _lastResponse = await HttpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);
            UpdateContentLength(_lastResponse);
            return _lastResponse;
        }

        /// <summary>
        /// Updates the content length based on the response.
        /// </summary>
        /// <param name="res">The response message.</param>
        private void UpdateContentLength(HttpResponseMessage res)
        {
            if (!res.IsSuccessStatusCode || !res.Content.Headers.ContentLength.HasValue)
                return;

            long length = res.Content.Headers.ContentLength.Value;
            if (Range.IsEmpty && FullContentLength != length && _addToStart == null)
                _contentLength = new(length);
            else if ((!Range.IsEmpty) && PartialContentLength != length)
                PartialContentLength = length;

        }

        /// <summary>
        /// Gets a cancellation token with a timeout if one is set; otherwise, gets the default cancellation token.
        /// </summary>
        /// <returns>The cancellation token.</returns>
        private CancellationToken TimedTokenOrDefault()
        {
            if (Timeout.HasValue)
            {
                CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
                cts.CancelAfter(Timeout.Value);
                return cts.Token;
            }
            return Token;
        }

        /// <summary>
        /// Initializes the start of the range.
        /// </summary>
        private void InitAddToStart() => _addToStart = (_range.Start.HasValue || _secondRange.Start.HasValue ? 0 : null);

        /// <summary>
        /// Sets the range and second range of <see cref="Options"/> to a fitting value for the request.
        /// </summary>
        /// <param name="length">The length of the content.</param>
        private void SetRange(long length)
        {
            if (!Range.IsEmpty && !_secondRange.IsEmpty)
            {
                LoadRange range = LoadRange.ToAbsolut(Range, length, out _);
                LoadRange secRange = LoadRange.ToAbsolut(_secondRange, length, out _);
                long start = Math.Max(range.Start ?? 0, secRange.Start ?? 0);
                long end = Math.Min(range.End ?? long.MaxValue, secRange.End ?? long.MaxValue);
                _range = new LoadRange(start == 0 ? null : start, end == long.MaxValue ? null : end);
                PartialContentLength = _range.Length;
            }
            else
            {
                _range = LoadRange.ToAbsolut(Range.IsEmpty ? _secondRange : Range, length, out long? partLength);
                PartialContentLength = partLength;
            }
        }

        /// <summary>
        /// Adds a specified value to the start of the range for the request. The start value does not change.
        /// </summary>
        /// <param name="length">The length to add to the start of the range.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown when the start of the range plus the length is greater than or equal to the end of the range.</exception>
        public void AddBytesToStart(long length)
        {
            if (Range.End.HasValue && (Range.Start ?? 0 + length) >= Range.End.Value)
                throw new IndexOutOfRangeException($"{nameof(Range.Start)} + {nameof(length)} can not be longer or equal than {nameof(Range.End)}");
            if (length <= 0)
                _addToStart = _range.Start.HasValue ? 0 : null;
            else
                _addToStart = length;
        }

        /// <summary>
        /// Checks if the last request was partial.
        /// </summary>
        /// <returns>
        /// A boolean value indicating whether the last response status code was partial.
        /// </returns>
        public bool IsPartial() => _lastResponse?.StatusCode == System.Net.HttpStatusCode.PartialContent;

        /// <summary>
        /// Determines if the next request should be partial.
        /// </summary>
        /// <returns>
        /// A boolean value indicating whether the Range is not empty or there is a value to add to the start.
        /// </returns>
        public bool HasToBePartial() => !Range.IsEmpty || _addToStart > 0;


        /// <summary>
        /// Clones a HttpRequestMessage.
        /// </summary>
        /// <param name="req">The HttpRequestMessage to clone.</param>
        /// <returns>
        /// A new HttpRequestMessage with the same properties as the original.
        /// <see cref="System.Net.Http.HttpRequestMessage"/> is used to create a new request message.
        /// </returns>
        private static HttpRequestMessage CloneRequestMessage(HttpRequestMessage req)
        {
            HttpRequestMessage clone = new(req.Method, req.RequestUri) { Version = req.Version };

            foreach (KeyValuePair<string, object?> option in req.Options)
                clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);

            foreach (KeyValuePair<string, IEnumerable<string>> header in req.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return clone;
        }

        /// <summary>
        /// Disposes this object and suppresses finalization.
        /// </summary>
        /// <remarks>
        /// If the object has already been disposed, no action is taken.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _originalMessage.Dispose();
            _lastResponse?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
