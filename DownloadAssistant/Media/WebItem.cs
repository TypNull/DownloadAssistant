

using DownloadAssistant.Options;
using DownloadAssistant.Request;

namespace DownloadAssistant.Media
{
    /// <summary>
    /// Web item with Information of the file
    /// </summary>
    public record WebItem
    {
        /// <summary>
        /// Constructor of WebItem
        /// </summary>
        /// <param name="url">The URL of the Item</param>
        /// <param name="title">Title of the Item</param>
        /// <param name="description">Description of the Item</param>
        /// <param name="typeRaw">Raw Media type of the Item</param>
        public WebItem(Uri url, string title, string description, string typeRaw)
        {
            URL = url;
            Description = description;
            Title = title;
            Type = new(typeRaw);
        }

        /// <summary>
        /// Description of the WebItem
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Uri that holds Url of Webitem
        /// </summary>
        public Uri URL { get; init; }

        /// <summary>
        /// Title of the WebItem
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// Type of the WebItem
        /// </summary>
        public WebType Type { get; init; }

        /// <summary>
        /// Creates a <see cref="GetRequest"/> out of this WebItem
        /// </summary>
        /// <param name="requestOptions">Options for the Request</param>
        /// <returns>Returns a new <see cref="GetRequest"/></returns>

        public GetRequest CreateLoadRequest(GetRequestOptions? requestOptions = null) => new(URL.AbsoluteUri, requestOptions);

        /// <summary>
        /// Creates a <see cref="StatusRequest"/> to see if the file is available.
        /// </summary>
        /// <param name="requestOptions">Options for the Request</param>
        /// <returns>A <see cref="StatusRequest"/></returns>
        public StatusRequest CreateStatusRequest(WebRequestOptions<HttpResponseMessage>? requestOptions = null) => new(URL.AbsoluteUri, requestOptions);

    }
}
