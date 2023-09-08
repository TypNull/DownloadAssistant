using Requests;
using Requests.Options;
using System.Net;

namespace DownloadAssistant.Options
{
    /// <summary>
    /// An implementation of an IWebRequestOptions as generic
    /// </summary>
    /// <typeparam name="TCompleated">Type of return if compleated</typeparam>
    public record WebRequestOptions<TCompleated> : RequestOptions<TCompleated, HttpResponseMessage?>, IWebRequestOptions<TCompleated>
    {
        /// <summary>
        /// If the <see cref="Request"/> is an big file and sold download in a second <see cref="Thread"/>.
        /// Can't be used if <see cref="RequestHandler"/> is manually set.
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
        /// Main constructor for the record <see cref="WebRequestOptions{TDelegateCompleated}"/>
        /// </summary>
        public WebRequestOptions() { }

        /// <summary>
        /// Copy constructor for the record <see cref="WebRequestOptions{TDelegateCompleated}"/>
        /// </summary>
        /// <param name="options">Options to copy</param>
        protected WebRequestOptions(WebRequestOptions<TCompleated> options) : base(options)
        {
            IsDownload = options.IsDownload;
            Timeout = options.Timeout;
            if (options.Headers != null)
                foreach (string key in options.Headers.AllKeys)
                    Headers?.Add(key, Headers[key]);
            UserAgent = options.UserAgent;
        }
    }
}
