using DownloadAssistant.Base;
using DownloadAssistant.Media;
using DownloadAssistant.Options;
using System.Text.RegularExpressions;

namespace DownloadAssistant.Requests
{

    /// <summary>
    /// Enhanced website scanning request with improved HTML parsing, resource detection, and error handling.
    /// </summary>
    public class SiteRequest : WebRequest<WebRequestOptions<SiteRequest>, SiteRequest>
    {
        private const string TagRegex = @"<(\w+)[^>]*>";
        private const string AttributeRegex = @"(\w+)\s*=\s*[""']?([^""'\s>]+)[""']?";
        private const string UrlRegex = @"^(http|ftp|https):\/\/.*";
        private const string SrcHrefRegex = @"(src|href|data-src|poster)\s*=\s*[""']([^""']*)[""']";
        private const string StyleUrlRegex = @"url\(\s*[""']?([^""')]+)[""']?\s*\)";
        private const string LinkTagRegex = @"<link[^>]+href\s*=\s*[""']([^""']*)[""'][^>]*>";
        private const string ScriptTagRegex = @"<script[^>]+src\s*=\s*[""']([^""']*)[""'][^>]*>";

        /// <summary>
        /// Gets the HTML content of the website.
        /// </summary>
        public string HTML { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the base URL of the website.
        /// </summary>
        public string BaseUrl { get; private set; } = string.Empty;

        /// <summary>
        /// Gets all the links on the website.
        /// </summary>
        public IReadOnlyList<WebItem> Links { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the videos on the website.
        /// </summary>
        public IReadOnlyList<WebItem> Videos { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the audios on the website.
        /// </summary>
        public IReadOnlyList<WebItem> Audios { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the images on the website.
        /// </summary>
        public IReadOnlyList<WebItem> Images { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the files on the website.
        /// </summary>
        public IReadOnlyList<WebItem> Files { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the scripts on the website.
        /// </summary>
        public IReadOnlyList<WebItem> Scripts { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the unknown type files on the website.
        /// </summary>
        public IReadOnlyList<WebItem> UnknownType { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the CSS files on the website.
        /// </summary>
        public IReadOnlyList<WebItem> CSS { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Gets all the references on the website.
        /// </summary>
        public IReadOnlyList<WebItem> References { get; private set; } = new List<WebItem>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SiteRequest"/> class.
        /// </summary>
        /// <param name="url">The URL for the <see cref="SiteRequest"/>.</param>
        /// <param name="options">The options that configure the <see cref="SiteRequest"/>.</param>
        /// <exception cref="UriFormatException">Thrown when the provided URL is not well-formed.</exception>
        public SiteRequest(string url, WebRequestOptions<SiteRequest>? options = null) : base(url, options)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                throw new UriFormatException($"Invalid URL format: {url}");

            BaseUrl = new Uri(url).GetLeftPart(UriPartial.Authority);
            AutoStart();
        }

        /// <summary>
        /// Runs the request asynchronously.
        /// </summary>
        protected override async Task<RequestReturn> RunRequestAsync()
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await SendHttpMessage();

                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/html")
                    return new RequestReturn(false, this, response);

                HTML = await response.Content.ReadAsStringAsync();

                List<WebItem> resources = FindAllResources(HTML);
                CategorizeResources(resources);

                return new RequestReturn(true, this, response);
            }
            catch (Exception ex)
            {
                AddException(ex);
                return new RequestReturn(false, this, response);
            }
            finally
            {
                response?.Dispose();
            }
        }

        private async Task<HttpResponseMessage> SendHttpMessage()
        {
            HttpRequestMessage request = new(HttpMethod.Get, Url);
            if (Options.Timeout.HasValue)
            {
                CancellationTokenSource timeoutCTS = new();
                timeoutCTS.CancelAfter(Options.Timeout.Value);
                return await HttpGet.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCTS.Token);
            }
            return await HttpGet.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Token);
        }



        private void AddMatch(string html, string pattern, int group, List<WebItem> resources)
        {
            foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase))
            {
                string url = match.Groups[group].Value;
                NormalizeAndAddResource(url, resources);
            }
        }

        private List<WebItem> FindAllResources(string html)
        {
            List<WebItem> resources = new();
            AddMatch(html, SrcHrefRegex, 2, resources);
            AddMatch(html, StyleUrlRegex, 1, resources);
            AddMatch(html, LinkTagRegex, 2, resources);
            AddMatch(html, ScriptTagRegex, 2, resources);

            foreach (Match tagMatch in Regex.Matches(html, TagRegex, RegexOptions.IgnoreCase))
            {
                string tagContent = tagMatch.Value;
                foreach (Match attributeMatch in Regex.Matches(tagContent, AttributeRegex, RegexOptions.IgnoreCase))
                {
                    string attributeValue = attributeMatch.Groups[2].Value;
                    if (Regex.IsMatch(attributeValue, UrlRegex))
                        NormalizeAndAddResource(attributeValue, resources);
                }
            }
            return resources.DistinctBy(x => x.URL).ToList();
        }

        private void NormalizeAndAddResource(string url, List<WebItem> resources)
        {
            if (string.IsNullOrEmpty(url)) return;

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                url = BaseUrl + (url.StartsWith('/') ? "" : "/") + url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return;

            string type = GetMediaType(uri);
            resources.Add(new WebItem(uri, Path.GetFileName(uri.AbsolutePath), string.Empty, type));
        }

        private void CategorizeResources(List<WebItem> resources)
        {
            ResourceCategorizer categorizer = new();
            categorizer.Categorize(resources);

            Images = categorizer.Images;
            Videos = categorizer.Videos;
            Audios = categorizer.Audios;
            Links = categorizer.Links;
            Scripts = categorizer.Scripts;
            CSS = categorizer.CSS;
            UnknownType = categorizer.UnknownType;
            Files = categorizer.Files;
        }

        private static string GetMediaType(Uri uri)
        {
            string extension = Path.GetExtension(uri.AbsolutePath).ToLower();
            return MimeTypeMap.GetMimeType(extension);
        }
    }
}
