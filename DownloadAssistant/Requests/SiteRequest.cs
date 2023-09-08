using DownloadAssistant.Base;
using DownloadAssistant.Media;
using DownloadAssistant.Options;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DownloadAssistant.Request
{
    /// <summary>
    /// Request that scanns a Website for references and files.
    /// </summary>
    public class SiteRequest : WebRequest<WebRequestOptions<SiteRequest>, SiteRequest>
    {
        private const string URL_REGEX = "(http|ftp|https):\\/\\/([\\w_-]+(?:(?:\\.[\\w_-]+)+))([\\w.,@?^=%&:\\/~+#-]*[\\w@?^=%&\\/~+#-])";
        private const string REF_REGEX = "<.*?(src|SRC)\\s*=\\s*[\"\"'][^\"\"'#>]+[\"\"'].*?>";
        private const string REFLINK_REGEX = "<a.*?(href|HREF)\\s*=\\s*[\"\"'][^\"\"'#>]+[\"\"'].*?</a>";

        /// <summary>
        /// A <see cref="CancellationTokenSource"/> that will be used to let <see cref="WebRequestOptions{TCompleated}.Timeout"/> run.
        /// </summary>
        private CancellationTokenSource? _timeoutCTS;

        /// <summary>
        /// HTML of the WebSite.
        /// </summary>
        public string HTML { get; private set; } = string.Empty;

        /// <summary>
        /// Url of the WebSite.
        /// </summary>
        public string BaseURl { get; private set; } = string.Empty;

        /// <summary>
        /// All links on the Website
        /// </summary>
        public IReadOnlyList<WebItem> Links { get; private set; } = new List<WebItem>();
        /// <summary>
        /// All videos on the Website
        /// </summary>
        public IReadOnlyList<WebItem> Videos { get; private set; } = new List<WebItem>();
        /// <summary>
        /// All audios on the Website
        /// </summary>
        public IReadOnlyList<WebItem> Audios { get; private set; } = new List<WebItem>();
        /// <summary>
        /// All images on the Website
        /// </summary>
        public IReadOnlyList<WebItem> Images { get; private set; } = new List<WebItem>();
        /// <summary>
        /// All files on the Website
        /// </summary>
        public IReadOnlyList<WebItem> Files { get; private set; } = new List<WebItem>();
        /// <summary>
        /// All scrips on the Website
        /// </summary>
        public IReadOnlyList<WebItem> Scripts { get; private set; } = new List<WebItem>();
        /// <summary>
        /// All unknown type files on the Website
        /// </summary>
        public IReadOnlyList<WebItem> UnknownType { get; private set; } = new List<WebItem>();
        /// <summary>
        /// All css files on the Website
        /// </summary>
        public IReadOnlyList<WebItem> CSS { get; private set; } = new List<WebItem>();
        /// <summary>
        /// All referneces on the Website
        /// </summary>
        public IReadOnlyList<WebItem> References { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Main contructor of <see cref="SiteRequest"/>
        /// </summary>
        /// <param name="url">Url of <see cref="SiteRequest"/></param>
        /// <param name="options">Options that configure the <see cref="SiteRequest"/></param>
        /// <exception cref="UriFormatException">Throws a exception if url is not well formated</exception>
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

        /// <inheritdoc/>
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

        private void ScanReferences()
        {
            List<string> found = new Regex(REF_REGEX, RegexOptions.Multiline, TimeSpan.FromSeconds(10)).Matches(HTML).Select(x => x.Value).ToList();
            found.AddRange(new Regex(REFLINK_REGEX, RegexOptions.Multiline, TimeSpan.FromSeconds(10)).Matches(HTML).Select(x => x.Value).ToList());

            IEnumerable<WebItem> references = ScanReferences(found.ToArray()).DistinctBy(x => x.URL.AbsoluteUri);

            ScanOther(references.Select(x => x.URL.AbsoluteUri));
            References = references.ToList();
            SortReferneces();
        }

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

        private static string GetTag(string nameRegex, string value)
        {
            if (Regex.Match(value, $@"({nameRegex})\s*=\""(.*?)\""", RegexOptions.Singleline) is Match match && match.Success)
                return match.Groups[2].Value;
            else return string.Empty;
        }
    }
}
