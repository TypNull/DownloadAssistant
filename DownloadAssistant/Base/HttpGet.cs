using System;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace DownloadAssistant.Base
{
    /// <summary>
    /// Class to Create a HttpGet
    /// </summary>
    public partial class HttpGet : IDisposable
    {
        /// <summary>
        /// Sets the download range of the Request
        /// </summary>
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
        /// Sets a second range and the inner values between Range and second range will be set as new Range
        /// </summary>
        public LoadRange SecondRange { init { _secondRange = value; InitAddToStart(); } }
        private readonly LoadRange _secondRange = new();
        private long? _addToStart;

        /// <summary>
        /// TimeSpan to cancel download after time
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// CancellationToken of this download
        /// </summary>
        public CancellationToken Token { get; init; }


        /// <summary>
        /// Contains an <see cref="Exception"/> if the head Request failed
        /// </summary>
        public Exception? HeadRequestException { get; private set; }

        private readonly HttpRequestMessage _originalMessage;
        private HttpResponseMessage? _lastResponse;

        /// <summary>
        /// Length of the content of response
        /// </summary>
        public long? FullContentLength => _contentLength.IsValueCreated ? _contentLength.Value : null;

        private Lazy<long> _contentLength;
        private bool _disposed;

        /// <summary>
        /// Length of the part of the content that will be recived when Range is set.
        /// </summary>
        public long? PartialContentLength { get; private set; }


        /// <summary>
        /// Creates a HttpGet
        /// </summary>
        /// <param name="msg">message to call</param>
        /// <param name="supportHeadRequest">If the server does not support head requests set value to false</param>
        /// <exception cref="NotSupportedException">Throws exeption if it is not a HttpMethod.Get</exception>
        public HttpGet(HttpRequestMessage msg, bool supportHeadRequest = true)
        {
            if (msg.Method != HttpMethod.Get)
                throw new NotSupportedException($"The HttpMethod in HttpRequestMessage can only be set to HttpMethod.Get");
            ArgumentNullException.ThrowIfNull(msg.RequestUri);

            _originalMessage = msg;
            _contentLength = supportHeadRequest ? new(LoadContentLength) : new(0L);
        }

        /// <summary>
        /// Preset the ContentLength
        /// </summary>
        /// <param name="contentLength">length to set</param>
        public void SetContentLength(long contentLength)
        {
            _contentLength = new(contentLength);
            if (!Range.IsEmty)
                SetRange(FullContentLength!.Value);
        }

        /// <summary>
        /// Instanziates the Lazy <see cref="_contentLength"/>.
        /// Called only one time
        /// </summary>
        /// <returns>A nullable Long that wants to contain the length of the content</returns>
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
            if (!Range.IsEmty && length > 0)
                SetRange(length);
            return length;
        }

        /// <summary>
        /// Gets the content length from the server
        /// </summary>
        /// <returns></returns>
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
        /// Create the HttpResponseMessage
        /// </summary>
        /// <returns>retruns response</returns>
        public async Task<HttpResponseMessage> LoadResponseAsync()
        {
            if (!IsLengthSet(out HttpResponseMessage res))
                return res;

            HttpRequestMessage msg = CloneRequestMessage(_originalMessage);
            if (HasToBePartial())
                msg.Headers.Range = new RangeHeaderValue((Range.Start ?? 0) + (_addToStart ?? 0), Range.End);

            return await SendHttpMenssage(msg);
        }

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
                    if (!Range.IsEmty)
                        SetRange(FullContentLength!.Value);
                }
                else
                    return false;
            }
            res = new();
            return true;
        }

        private async Task<HttpResponseMessage> SendHttpMenssage(HttpRequestMessage msg)
        {
            _lastResponse?.Dispose();
            CancellationToken token = TimedTokenOrDefault();
            _lastResponse = await HttpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);
            UpdateContentLength(_lastResponse);
            return _lastResponse;
        }

        private void UpdateContentLength(HttpResponseMessage res)
        {
            if (!res.IsSuccessStatusCode || !res.Content.Headers.ContentLength.HasValue)
                return;

            long length = res.Content.Headers.ContentLength.Value;
            if (Range.IsEmty && FullContentLength != length && _addToStart == null)
                _contentLength = new(length);
            else if ((!Range.IsEmty) && PartialContentLength != length)
                PartialContentLength = length;

        }

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

        private void InitAddToStart() => _addToStart = (_range.Start.HasValue || _secondRange.Start.HasValue ? 0 : null);

        /// <summary>
        /// Sets the Range and second Range of <see cref="Options"/> to a fitting value for the request
        /// </summary>
        /// <param name="length">legth of the content</param>
        /// <returns>length of the content fitting to the Range</returns>
        private void SetRange(long length)
        {
            if (!Range.IsEmty && !_secondRange.IsEmty)
            {
                LoadRange range = RangeToAbsolut(Range, length, out _);
                LoadRange secRange = RangeToAbsolut(_secondRange, length, out _);
                _range = new LoadRange(Math.Max(range.Start ?? 0, secRange.Start ?? 0),
                    Math.Min(range.End ?? 0, secRange.End ?? 0));
                PartialContentLength = _range.Length;
            }
            else
            {
                _range = RangeToAbsolut(Range.IsEmty ? _secondRange : Range, length, out long? partLength);
                PartialContentLength = partLength;
            }
        }

        private static LoadRange RangeToAbsolut(LoadRange range, long length, out long? partialLength)
        {
            LoadRange absolutRange = range;
            if (range.IsAbsolut)
            {
                if (range.Length > length)
                    absolutRange = new(range.Start, null);

                if (range.End == null)
                    partialLength = length - range.Start;
                else
                    partialLength = range.Length;
            }
            else if (range.IsPromille)
            {
                decimal onePromill = (decimal)length / 1000;
                partialLength = (long?)(onePromill * (range.End ?? 1000 - range.Start));
                long? startIndex = (long?)(onePromill * range.Start);
                absolutRange = new LoadRange(startIndex == 0 ? startIndex : startIndex + 1, (long?)(onePromill * range.End));
            }
            else
            {
                decimal? partLength = (decimal)length / range.Length!.Value;
                long? startIndex = (long?)(partLength * range.Start);
                absolutRange = new LoadRange(startIndex == 0 ? startIndex : startIndex + 1, (long?)(partLength * range.End));
                partialLength = absolutRange.Length;
            }
            return absolutRange;
        }

        /// <summary>
        /// Adds the value to the start value of Range for the request.
        /// The start value does not change
        /// </summary>
        /// <param name="length"></param>
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
        /// If the last request was partial
        /// </summary>
        /// <returns>bool</returns>
        public bool IsPartial() => _lastResponse?.StatusCode == System.Net.HttpStatusCode.PartialContent;

        /// <summary>
        /// If the last request should be partial
        /// </summary>
        /// <returns>bool</returns>
        public bool HasToBePartial() => !Range.IsEmty || _addToStart > 0;


        /// <summary>
        /// Clones a HttpRequestMessage
        /// </summary>
        /// <param name="req">HttpRequestMessage to clone</param>
        /// <returns>A new HttpRequestMessage</returns>
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
        /// Dispose this object
        /// </summary>
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
