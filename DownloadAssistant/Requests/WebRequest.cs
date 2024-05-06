using DownloadAssistant.Options;
using Requests;

namespace DownloadAssistant.Requests
{
    /// <summary>
    /// Serves as the base class for all <see cref="WebRequest{TOptions, TCompleated}"/> implementations that use the HttpClient.
    /// </summary>
    /// <typeparam name="TOptions">Specifies the type of options used by the web request.</typeparam>
    /// <typeparam name="TCompleated">Specifies the return type of the request.</typeparam>
    public abstract class WebRequest<TOptions, TCompleated> : Request<TOptions, TCompleated, HttpResponseMessage?> where TOptions : WebRequestOptions<TCompleated>, new()
    {
        /// <summary>
        /// Gets the <see cref="Uri"/> that represents the URL of the <see cref="WebRequest{TOptions, TCompleated}"/>.
        /// </summary>
        protected readonly Uri _uri;

        /// <summary>
        /// Gets the <see cref="string"/> that represents the URL of the <see cref="WebRequest{TOptions, TCompleated}"/>.
        /// </summary>
        public string Url => _uri.AbsoluteUri;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebRequest{TOptions, TCompleated}"/> class.
        /// </summary>
        /// <param name="url">The URL that the <see cref="WebRequest{TOptions, TCompleated}"/> will call.</param>
        /// <param name="options">The options used to modify the <see cref="WebRequest{TOptions, TCompleated}"/>.</param>
        public WebRequest(string url, TOptions? options) : base(options) => _uri = new(url);

        /// <summary>
        /// Sets the headers of the <see cref="WebRequestOptions{TCompleated}"/> to a <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <param name="httpRequest">The <see cref="HttpRequestMessage"/> to which the headers will be added.</param>
        /// <returns>A <see cref="HttpRequestMessage"/> with all the default headers set.</returns>
        protected HttpRequestMessage GetPresetRequestMessage(HttpRequestMessage? httpRequest = null)
        {
            httpRequest ??= new HttpRequestMessage();
            foreach (string key in Options.Headers?.AllKeys ?? Array.Empty<string>())
                httpRequest.Headers.Add(key, Options.Headers?[key]);

            if (!string.IsNullOrWhiteSpace(Options.UserAgent))
                httpRequest.Headers.Add("User-Agent", Options.UserAgent);
            return httpRequest;
        }
    }
}
