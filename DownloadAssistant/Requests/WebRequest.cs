using DownloadAssistant.Options;
using Requests;

namespace DownloadAssistant.Request
{
    /// <summary>
    /// Base class for all <see cref="WebRequest{TOptions, TCompleated}"/> implementations that are using the HttpClient
    /// </summary>
    /// <typeparam name="TOptions">The Options type</typeparam>
    /// <typeparam name="TCompleated">return type of request</typeparam>
    public abstract class WebRequest<TOptions, TCompleated> : Request<TOptions, TCompleated, HttpResponseMessage?> where TOptions : WebRequestOptions<TCompleated>, new()
    {

        /// <summary>
        /// <see cref="Uri"/> that holds the URL of the <see cref="WebRequest{TOptions, TCompleated}"/>.
        /// </summary>
        protected readonly Uri _uri;
        /// <summary>
        /// <see cref="string"/> that holds the URL of the <see cref="WebRequest{TOptions, TCompleated}"/>.
        /// </summary>
        public string Url => _uri.AbsoluteUri;

        /// <summary>
        /// Consructor of the <see cref="WebRequest{TOptions, TCompleated}"/> class 
        /// </summary>
        /// <param name="url">URL that the <see cref="WebRequest{TOptions, TCompleated}"/> calls</param>
        /// <param name="options">Options to modify the <see cref="WebRequest{TOptions, TCompleated}"/></param>
        public WebRequest(string url, TOptions? options) : base(options) => _uri = new(url);

        /// <summary>
        /// Sets the Headers of <see cref="WebRequestOptions{TCompleated}"/> to a <see cref="HttpRequestMessage"/>
        /// </summary>
        /// <param name="httpRequest">A <see cref="HttpRequestMessage"/> that was created</param>
        /// <returns>A <see cref="HttpRequestMessage"/> with all default Headers</returns>
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
