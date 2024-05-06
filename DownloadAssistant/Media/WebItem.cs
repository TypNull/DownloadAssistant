

using DownloadAssistant.Options;
using DownloadAssistant.Requests;

namespace DownloadAssistant.Media
{
    /// <summary>
    /// Represents a web item with information about a file.
    /// </summary>
    public record WebItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebItem"/> class.
        /// </summary>
        /// <param name="url">The URL of the item.</param>
        /// <param name="title">The title of the item.</param>
        /// <param name="description">The description of the item.</param>
        /// <param name="typeRaw">The raw media type of the item.</param>
        public WebItem(Uri url, string title, string description, string typeRaw)
        {
            URL = url;
            Description = description;
            Title = title;
            Type = new(typeRaw);
        }

        /// <summary>
        /// Gets or sets the description of the web item.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets or sets the URL of the web item.
        /// </summary>
        public Uri URL { get; init; }

        /// <summary>
        /// Gets or sets the title of the web item.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// Gets or sets the type of the web item.
        /// </summary>
        public WebType Type { get; init; }

        /// <summary>
        /// Creates a <see cref="GetRequest"/> from this web item.
        /// </summary>
        /// <param name="requestOptions">The options for the request.</param>
        /// <returns>A new <see cref="GetRequest"/>.</returns>

        public GetRequest CreateLoadRequest(GetRequestOptions? requestOptions = null) => new(URL.AbsoluteUri, requestOptions);

        /// <summary>
        /// Creates a <see cref="StatusRequest"/> to check if the file is available.
        /// </summary>
        /// <param name="requestOptions">The options for the request.</param>
        /// <returns>A new <see cref="StatusRequest"/>.</returns>
        public StatusRequest CreateStatusRequest(WebRequestOptions<HttpResponseMessage>? requestOptions = null) => new(URL.AbsoluteUri, requestOptions);

    }
}
