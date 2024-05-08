using DownloadAssistant.Base;
using DownloadAssistant.Media;
using DownloadAssistant.Options;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DownloadAssistant.Requests
{
    /// <summary>
    /// Request that scanns a Website for references and files.
    /// </summary>
    public class SiteRequest : WebRequest<WebRequestOptions<SiteRequest>, SiteRequest>
    {
        // Regular expressions for URL, reference, and reference link.
        private const string URL_REGEX = "(http|ftp|https):\\/\\/([\\w_-]+(?:(?:\\.[\\w_-]+)+))([\\w.,@?^=%&:\\/~+#-]*[\\w@?^=%&\\/~+#-])";
        private const string REF_REGEX = "<.*?(src|SRC)\\s*=\\s*[\"\"'][^\"\"'#>]+[\"\"'].*?>";
        private const string REFLINK_REGEX = "<a.*?(href|HREF)\\s*=\\s*[\"\"'][^\"\"'#>]+[\"\"'].*?</a>";
        /// <summary>
        /// A <see cref="CancellationTokenSource"/> used to handle timeouts in <see cref="WebRequestOptions{TCompleated}.Timeout"/>.
        /// </summary>
        private CancellationTokenSource? _timeoutCTS;

        /// <summary>
        /// Gets the HTML content of the website.
        /// </summary>
        /// <value>The HTML content of the website.</value>
        public string HTML { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the base URL of the website.
        /// </summary>
        /// <value>The base URL of the website.</value>
        public string BaseURl { get; private set; } = string.Empty;

        /// <summary>
        /// Gets all the links on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the links on the website.</value>
        public IReadOnlyList<WebItem> Links { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the videos on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the videos on the website.</value>
        public IReadOnlyList<WebItem> Videos { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the audios on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the audios on the website.</value>
        public IReadOnlyList<WebItem> Audios { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the images on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the images on the website.</value>
        public IReadOnlyList<WebItem> Images { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the files on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the files on the website.</value>
        public IReadOnlyList<WebItem> Files { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the scripts on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the scripts on the website.</value>
        public IReadOnlyList<WebItem> Scripts { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the unknown type files on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the unknown type files on the website.</value>
        public IReadOnlyList<WebItem> UnknownType { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the CSS files on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the CSS files on the website.</value>
        public IReadOnlyList<WebItem> CSS { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the references on the website.
        /// </summary>
        /// <value>A read-only list of <see cref="WebItem"/> representing all the references on the website.</value>
        public IReadOnlyList<WebItem> References { get; private set; } = new List<WebItem>();


        /// <summary>
        /// Initializes a new instance of the <see cref="SiteRequest"/> class.
        /// </summary>
        /// <param name="url">The URL for the <see cref="SiteRequest"/>.</param>
        /// <param name="options">The options that configure the <see cref="SiteRequest"/>.</param>
        /// <exception cref="UriFormatException">Thrown when the provided URL is not well-formed.</exception>
        public SiteRequest(string url, WebRequestOptions<SiteRequest>? options = null) : base(url, options)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
                throw new UriFormatException($"The {url} has not the right format");
            string host = _uri.Host;
            if (!host.Contains("http://") && !host.Contains("https://"))
                BaseURl = "https://" + host;
            else
                BaseURl = host;

            AutoStart();
        }

        /// <summary>
        /// Sends an HTTP message.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task<HttpResponseMessage> SendHttpMenssage()
        {
            HttpRequestMessage msg = GetPresetRequestMessage(new(HttpMethod.Get, Url));
            if (Options.Timeout.HasValue)
            {
                _timeoutCTS?.Dispose();
                _timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(Token);
                _timeoutCTS.CancelAfter(Options.Timeout.Value);
            }
            return await HttpGet.HttpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, Token);
        }

        /// <summary>
        /// Runs the request asynchronously.
        /// </summary>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
        protected override async Task<RequestReturn> RunRequestAsync()
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await SendHttpMenssage();

                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/html")
                    return new(false, this, response);

                HTML = await response.Content.ReadAsStringAsync();

                ScanReferences();

                response?.Dispose();

                return new(true, this, response);
            }
            catch (Exception ex)
            {
                AddException(ex);
                Debug.Assert(false, ex.Message);
                response?.Dispose();
            }
            return new(false, this, response);
        }

        /// <summary>
        /// Scans the HTML for references.
        /// </summary>
        private void ScanReferences()
        {
            List<string> found = new Regex(REF_REGEX, RegexOptions.Multiline, TimeSpan.FromSeconds(10)).Matches(HTML).Select(x => x.Value).ToList();
            found.AddRange(new Regex(REFLINK_REGEX, RegexOptions.Multiline, TimeSpan.FromSeconds(10)).Matches(HTML).Select(x => x.Value).ToList());

            IEnumerable<WebItem> references = ScanReferences(found.ToArray()).DistinctBy(x => x.URL.AbsoluteUri);

            ScanOther(references.Select(x => x.URL.AbsoluteUri));
            References = references.ToList();
            SortReferneces();
        }

        /// <summary>
        /// Sorts the references by type.
        /// </summary>
        private void SortReferneces()
        {
            Files = References.Where(reference => reference.Type.IsMedia).ToList();
            Links = References.Where(reference => reference.Type.IsLink).ToList();
            Scripts = References.Where(reference => reference.Type.Extension == "javascript").ToList();
            CSS = References.Where(reference => reference.Type.Extension == "css").ToList();
            Audios = References.Where(reference => reference.Type.IsAudio).ToList();
            Videos = References.Where(reference => reference.Type.IsVideo).ToList();
            UnknownType = References.Where(reference => reference.Type.IsUnknown).ToList();
            Images = References.Where(reference => reference.Type.IsImage).ToList();
        }

        /// <summary>
        /// Scans for other items in the HTML.
        /// </summary>
        /// <param name="found">The found items.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="WebItem"/>.</returns>
        private IEnumerable<WebItem> ScanOther(IEnumerable<string> found)
        {
            List<WebItem> others = new();
            IEnumerable<string> othersURLs = new Regex(URL_REGEX, RegexOptions.Multiline, TimeSpan.FromSeconds(10)).Matches(HTML).Select(x => x.Value);
            IEnumerable<string> differences = othersURLs.Except(found);
            foreach (string url in differences)
            {
                if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
                    continue;
                Uri uri = new(url);
                string type = FileExtensionToMediaTag(uri);
                others.Add(new(uri, Path.GetFileName(uri.AbsolutePath), "", type));
            }
            return others;
        }

        /// <summary>
        /// Scans the tags for references.
        /// </summary>
        /// <param name="tags">The tags to scan.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="WebItem"/>.</returns>
        private IEnumerable<WebItem> ScanReferences(string[] tags)
        {
            List<WebItem> references = new();

            foreach (string tag in tags)
            {
                string description = Regex.Replace(tag, @"\s*<.*?>\s*", "", RegexOptions.Singleline);

                string title = GetTag("title|TITLE", tag);
                string type = GetTag("type|TYPE", tag); ;
                string url;

                if (Regex.IsMatch(tag, @"(<a.*?/?>.*?</a>)"))
                {
                    url = GetTag("href|HREF", tag);
                    type = "link/url";
                }
                else
                {
                    url = GetTag("src|SRC", tag);
                    if (string.IsNullOrWhiteSpace(title))
                        title = GetTag("alt|ALT", tag);
                }

                if (string.IsNullOrWhiteSpace(url))
                    continue;
                if (!url.Contains("http"))
                    url = BaseURl + (url.StartsWith("/") ? url : "/" + url);

                if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
                    continue;
                Uri uri = new(url);
                if (type == string.Empty)
                    type = FileExtensionToMediaTag(uri);
                references.Add(new(uri, title, description, type));
            }
            return references;
        }

        /// <summary>
        /// Converts the file extension of a URI to a media tag.
        /// </summary>
        /// <param name="uri">The URI to extract the file extension from.</param>
        /// <returns>A string representing the media tag.</returns>
        private static string FileExtensionToMediaTag(Uri uri)
        {
            if (!Path.HasExtension(uri.AbsolutePath))
                return "application/octet-stream";
            string ext = Path.GetExtension(uri.AbsolutePath).Replace(".", "").Trim();
            return ext switch
            {
                "jpeg" or "jpg" or "png" or "avif" or "webp" or "svg" or "tiff" or "gif" or "bmp" => "image/" + ext,
                "html" or "css" => "text/" + ext,
                "js" => "text/javascript",
                "pdf" or "json" or "zip" or "epub" or "mobi" or "doc" or "docx" or "bmp" => "application/" + ext,
                "mpeg" or "ogg" or "wav" or "mp3" => "audio/" + ext,
                "txt" => "text/plain",
                "mp4" or "ogg" or "mkv" or "ts" => "video/" + ext,
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Extracts the value of a specified tag from a string.
        /// </summary>
        /// <param name="nameRegex">The regular expression pattern to match the tag name.</param>
        /// <param name="value">The string to search for the tag.</param>
        /// <returns>The value of the specified tag if found; otherwise, an empty string.</returns>
        private static string GetTag(string nameRegex, string value)
        {
            if (Regex.Match(value, $@"({nameRegex})\s*=\""(.*?)\""", RegexOptions.Singleline) is Match match && match.Success)
                return match.Groups[2].Value;
            else return string.Empty;
        }
    }
}
