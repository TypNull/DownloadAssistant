using Requests.Options;
using System.Net;

namespace DownloadAssistant.Options
{
    /// <summary>
    /// An interface for all <see cref="WebRequest"/> instances.
    /// </summary>
    /// <typeparam name="TCompleated">Type of return if completed.</typeparam>
    public interface IWebRequestOptions<TCompleated> : IRequestOptions<TCompleated, HttpResponseMessage?>
    {
        /// <summary>
        /// Gets a value indicating whether the <see cref="WebRequest"/> is downloading a large file in a separate <see cref="Thread"/>.
        /// This will be ignored if <see cref="IRequestOptions{TCompleated, TFailed}.Handler"/> is manually set.
        /// </summary>
        public bool IsDownload { get; }

        /// <summary>
        /// Gets or sets the timeout duration for a single <see cref="WebRequest"/> attempt.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="string"/> containing the value of the HTTP User-agent header.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the header (key/value) pairs to be set for the <see cref="WebRequest"/>.
        /// </summary>
        public WebHeaderCollection Headers { get; set; }
    }
}
