using Requests;
using Requests.Options;
using System.Net;

namespace DownloadAssistant.Options
{
    /// <summary>
    /// An implementation of <see cref="IWebRequestOptions{TCompleated}"/> as a generic class.
    /// </summary>
    /// <typeparam name="TCompleated">Type of return if completed.</typeparam>
    public record WebRequestOptions<TCompleated> : RequestOptions<TCompleated, HttpResponseMessage?>, IWebRequestOptions<TCompleated>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="Request{TOptions, TCompleated, TFailed}"/> is downloading a large file in a separate <see cref="Thread"/>.
        /// This property can't be used if <see cref="RequestHandler"/> is manually set.
        /// </summary>
        public bool IsDownload
        {
            get => _isDownload; set
            {
                _isDownload = value;
                if (value && Handler == RequestHandler.MainRequestHandlers[0])
                    Handler = RequestHandler.MainRequestHandlers[1];
                else if (!value && Handler == RequestHandler.MainRequestHandlers[1])
                    Handler = RequestHandler.MainRequestHandlers[0];
            }
        }
        private bool _isDownload = false;

        ///<inheritdoc />
        public string UserAgent { get; set; } = string.Empty;
        ///<inheritdoc />
        public WebHeaderCollection Headers { get; set; } = new();
        ///<inheritdoc />
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Main constructor for the <see cref="WebRequestOptions{TCompleated}"/> record.
        /// </summary>
        public WebRequestOptions() { }

        /// <summary>
        /// Copy constructor for the <see cref="WebRequestOptions{TCompleated}"/> record.
        /// </summary>
        /// <param name="options">Options to copy.</param>
        protected WebRequestOptions(WebRequestOptions<TCompleated> options) : base(options)
        {
            IsDownload = options.IsDownload;
            Timeout = options.Timeout;
            Headers = new();
            foreach (string key in options.Headers.AllKeys)
                Headers.Add(key, Headers[key]);
            UserAgent = options.UserAgent;
        }
    }
}
