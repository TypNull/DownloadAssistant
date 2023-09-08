
using Requests.Options;
using System.Net;

namespace DownloadAssistant.Options
{
    /// <summary>
    /// An interface for all <see cref="WebRequest">WebRequests</see>
    /// </summary>
    /// / <typeparam name="TCompleated">Type of return if compleated</typeparam>
    public interface IWebRequestOptions<TCompleated> : IRequestOptions<TCompleated, HttpResponseMessage?>
    {
        /// <summary>
        /// If the <see cref="WebRequest"/> is an big file and sold download in a second <see cref="Thread"/>.
        /// Will be ignored if <see cref="IRequestOptions{TCompleated, TFailed}.Handler"/> is manually set.
        /// </summary>
        public bool IsDownload { get; }

        /// <summary>
        /// Timeout of one <see cref="WebRequest"/> attemp.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// A <see cref="string" /> containing the value of the HTTP User-agent header.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Header (key/value) pairs set to the <see cref="WebRequest"/>.
        /// </summary>
        public WebHeaderCollection Headers { get; set; }
    }
}
