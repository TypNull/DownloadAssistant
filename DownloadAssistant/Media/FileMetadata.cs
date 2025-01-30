using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Web;

namespace DownloadAssistant.Media
{
    /// <summary>
    /// Provides file metadata extraction and sanitization capabilities for downloaded content.
    /// </summary>
    public class FileMetadata
    {
        private readonly HttpContentHeaders _headers;
        private readonly Uri _uri;

        private static readonly string[] DispositionHeaders = { /* IETF Standard */ "Content-Disposition", "X-Content-Disposition" };

        private static readonly string[] FilenameHeaders ={ 
            /* Cloud Provider Headers */  "X-Amz-Meta-Filename", "x-ms-meta-Filename", "X-Google-Filename",   
            /* Framework Headers */ "X-Django-FileName", "X-File-Key",    
            /* CDN/Proxy Headers */ "X-Original-Filename", "X-Source-Filename",
            /* Common Industry Headers */ "X-Filename","X-File-Name", "X-Object-Name"
        };

        private static readonly string[] RedirectHeaders = { 
            /* RFC 7231 Standard */ "Location",
            /* Nginx */ "X-Accel-Redirect",
            /* Apache */ "X-Sendfile"
        };

        private static readonly string[] QueryParamPriority =
        {
            /* Standard Parameters*/ "filename", "file", "name", "key",
            /* Platform-Specific Parameters */
            "file_name", "file-name", "googleStorageFileName", "azureFileName", "amzFileName"
        };

        private static readonly Dictionary<string, Func<string, string>> ContentHeaderMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Location"] = v => Path.GetFileName(new Uri(v).AbsolutePath),
            ["X-Content-Name"] = v => v,
            ["X-ShareFile-Name"] = v => Uri.UnescapeDataString(v)
        };

        /// <summary>
        /// Gets the sanitized filename for the downloaded content.
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the appropriate file extension based on content type or URI.
        /// </summary>
        public string Extension { get; private set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the FileMetadata class.
        /// </summary>
        /// <param name="headers">HTTP content headers from the response.</param>
        /// <param name="uri">Source URI of the downloaded content.</param>
        public FileMetadata(HttpContentHeaders headers, Uri uri)
        {
            _headers = headers;
            _uri = uri;
            SetFilename();
            SetExtension();
        }

        /// <summary>
        /// Determines the file extension from Content-Type header or URI path.
        /// </summary>
        private void SetExtension()
        {
            Extension = _headers.ContentType?.MediaType != null
                ? MimeTypeMap.GetDefaultExtension(_headers.ContentType.MediaType)
                : Path.GetExtension(_uri.AbsoluteUri);
        }

        /// <summary>
        /// Main filename determination workflow with fallback strategies.
        /// </summary>
        private void SetFilename()
        {
            FileName = GetFilenameFromHeaders()
                     ?? GetFilenameFromUri()
                     ?? GetFilenameFromAlternateSources()
                     ?? GenerateFallbackFilename();

            FileName = SanitizeFilename(FileName);
        }

        /// <summary>
        /// Attempts to extract filename from Content-Disposition headers.
        /// </summary>
        /// <returns>Valid filename or null if not found.</returns>
        private string? GetFilenameFromHeaders()
        {
            ContentDispositionHeaderValue? contentDisposition = _headers.ContentDisposition;
            if (contentDisposition != null)
            {
                string? filename = contentDisposition.FileNameStar ?? contentDisposition.FileName;
                if (!string.IsNullOrWhiteSpace(filename))
                    return Uri.UnescapeDataString(filename.Trim('"', '\'').Trim());
            }

            return CheckAlternativeDispositionHeaders();
        }

        private string? GetFilenameFromUri()
        {
            try
            {
                string path = _uri.GetLeftPart(UriPartial.Path);
                Uri cleanUri = new(path);

                string filename = Path.GetFileName(cleanUri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(filename))
                    return Uri.UnescapeDataString(filename);

                string? patternMatch = cleanUri.Segments.LastOrDefault(s => s.Contains('.') && !s.EndsWith('/'))?.Trim('/');

                if (!string.IsNullOrWhiteSpace(patternMatch))
                    return patternMatch;

                string decodedUrl = HttpUtility.UrlDecode(cleanUri.AbsoluteUri);
                string decodedFilename = Path.GetFileName(decodedUrl);
                if (!string.IsNullOrWhiteSpace(decodedFilename))
                    return decodedFilename;
            }
            catch { /* Log error if needed */ }

            return null;
        }

        /// <summary>
        /// Checks alternative disposition headers for filename information.
        /// </summary>
        /// <returns>Valid filename or null if not found.</returns>
        private string? CheckAlternativeDispositionHeaders()
        {
            foreach (string header in DispositionHeaders)
            {
                if (!_headers.TryGetValues(header, out IEnumerable<string>? values)) continue;

                string? value = values.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(value)) continue;

                Match match = Regex.Match(value, @"filename\*?=([^;]+)", RegexOptions.IgnoreCase);
                if (match.Success) return Uri.UnescapeDataString(match.Groups[1].Value.Trim('"', '\'').Trim());

            }
            return null;
        }

        /// <summary>
        /// Coordinates multiple alternate filename discovery strategies.
        /// </summary>
        /// <returns>Valid filename or null if not found.</returns>
        private string? GetFilenameFromAlternateSources()
        {
            return ExtractFromQueryParameters()
                   ?? ExtractFromContentHeaders()
                   ?? ExtractFromRedirectHeaders()
                   ?? ExtractFromCustomHeaders()
                   ?? ExtractFromContentType();
        }

        /// <summary>
        /// Extracts filename from content-related headers (Content-Location, etc.).
        /// </summary>
        /// <returns>Valid filename or null if not found.</returns>
        private string? ExtractFromContentHeaders()
        {
            foreach (KeyValuePair<string, Func<string, string>> header in ContentHeaderMap)
            {
                if (!_headers.TryGetValues(header.Key, out IEnumerable<string>? values)) continue;

                string? value = values.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(value)) continue;

                try
                {
                    return Uri.UnescapeDataString(header.Value(value).Trim());
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Attempts to extract filename from redirect-related headers.
        /// </summary>
        /// <returns>Valid filename or null if not found.</returns>
        private string? ExtractFromRedirectHeaders()
        {
            foreach (string header in RedirectHeaders)
            {
                if (!_headers.TryGetValues(header, out IEnumerable<string>? values)) continue;

                string? value = values.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) continue;

                string filename = Path.GetFileName(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(filename))
                    return Uri.UnescapeDataString(filename);
            }
            return null;
        }

        /// <summary>
        /// Checks custom filename headers from various cloud providers and frameworks.
        /// </summary>
        /// <returns>Valid filename or null if not found.</returns>
        private string? ExtractFromCustomHeaders()
        {
            foreach (string header in FilenameHeaders)
            {
                if (!_headers.TryGetValues(header, out IEnumerable<string>? values)) continue;

                string? value = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(value))
                    return Uri.UnescapeDataString(value.Trim());
            }
            return null;
        }

        /// <summary>
        /// Analyzes query parameters for potential filename hints.
        /// </summary>
        /// <returns>Valid filename or null if not found.</returns>
        private string? ExtractFromQueryParameters()
        {
            try
            {
                System.Collections.Specialized.NameValueCollection queryParams = HttpUtility.ParseQueryString(_uri.Query);
                foreach (string param in QueryParamPriority)
                {
                    string? value = queryParams[param];
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    string decoded = HttpUtility.UrlDecode(value);
                    return Path.GetFileName(decoded).Trim();
                }
            }
            catch { /* Log error if needed */ }
            return null;
        }

        /// <summary>
        /// Extracts filename information from Content-Type header parameters.
        /// </summary>
        /// <returns>Valid filename or null if not found.</returns>
        private string? ExtractFromContentType()
        {
            if (_headers.ContentType?.MediaType == null) return null;

            ICollection<NameValueHeaderValue> parameters = _headers.ContentType.Parameters;
            NameValueHeaderValue? nameParam = parameters.FirstOrDefault(p =>
                p.Name.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals("filename", StringComparison.OrdinalIgnoreCase));

            if (nameParam != null && !string.IsNullOrWhiteSpace(nameParam.Value))
                return Uri.UnescapeDataString(nameParam.Value.Trim('"', '\''));

            if (!_headers.TryGetValues("X-Content-Type-Filename", out IEnumerable<string>? values)) return null;

            string? value = values.FirstOrDefault();
            return !string.IsNullOrWhiteSpace(value) ? Uri.UnescapeDataString(value.Trim()) : null;
        }

        /// <summary>
        /// Generates a fallback filename using timestamp and host information.
        /// </summary>
        /// <returns>A generated filename in the format "download_YYYYMMDD-HHMMSS_host".</returns>
        private string GenerateFallbackFilename()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string hostPart = RemoveInvalidFileNameChars(_uri.Host);
            return $"download_{timestamp}_{hostPart}";
        }

        /// <summary>
        /// Sanitizes and normalizes filenames with security considerations.
        /// </summary>
        /// <param name="fileName">Raw filename input.</param>
        /// <returns>Safe, normalized filename with OS-specific length constraints.</returns>
        private string SanitizeFilename(string fileName)
        {
            string cleanName = RemoveInvalidFileNameChars(fileName).Trim();
            if (string.IsNullOrEmpty(cleanName))
                return GenerateFallbackFilename();

            cleanName = Path.GetFileName(cleanName.Replace("..", "."));

            int maxLength = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 80 : 255;

            if (cleanName.Length <= maxLength) return cleanName;

            string extension = Path.GetExtension(cleanName);
            string baseName = Path.GetFileNameWithoutExtension(cleanName);

            int allowedBaseLength = maxLength - extension.Length;
            return allowedBaseLength <= 0
                ? cleanName[..maxLength]
                : $"{baseName[..allowedBaseLength]}{extension}";
        }

        /// <summary>
        /// Replaces the first occurrence of a substring in the given text with a specified replacement.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <param name="search">The substring to search for.</param>
        /// <param name="replace">The replacement string.</param>
        /// <returns>The modified text with the first occurrence replaced, or the original text if the substring is not found.</returns>
        private static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
                return text;
            return string.Concat(text.AsSpan(0, pos), replace, text.AsSpan(pos + search.Length));
        }

        /// <summary>
        /// Builds the filename based on the preset filename or the generated filename.
        /// </summary>
        /// <param name="preSetFilename">The preset filename.</param>
        /// <returns>The built filename.</returns>
        public string BuildFilename(string preSetFilename)
        {
            string fileName = preSetFilename;
            if (fileName == string.Empty || fileName == "*" || fileName == "*.*")
            {
                fileName = FileName;
                fileName = fileName.Contains('.') ? fileName : fileName + Extension;
            }
            else
            {
                if (fileName.Contains("*."))
                    fileName = ReplaceFirst(fileName, "*.", Path.GetFileNameWithoutExtension(FileName) + ".");
                if (fileName.Contains(".*"))
                    fileName = ReplaceFirst(fileName, ".*", Extension);
            }
            return fileName;
        }

        /// <summary>
        /// Replaces invalid filesystem characters from a filename string.
        /// </summary>
        /// <param name="input">Original filename.</param>
        /// <returns>Sanitized filename with invalid characters removed.</returns>
        public static string RemoveInvalidFileNameChars(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            return new string(input.Where(c => !invalidChars.Contains(c)).ToArray());
        }
    }
}