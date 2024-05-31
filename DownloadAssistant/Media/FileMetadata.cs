using System.Net.Http.Headers;
using System.Text;

namespace DownloadAssistant.Media
{
    /// <summary>
    /// Class to build a file metadata.
    /// </summary>
    public class FileMetadata
    {
        private readonly HttpContentHeaders _headers;
        private readonly Uri _uri;

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the extension of the file.
        /// </summary>
        public string Extension { get; private set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileMetadata"/> class.
        /// </summary>
        /// <param name="headers"><see cref="HttpContentHeaders"/> of the response.</param>
        /// <param name="uri">The URL to the file.</param>
        public FileMetadata(HttpContentHeaders headers, Uri uri)
        {
            _headers = headers;
            _uri = uri;
            SetFilename();
            SetExtension();
        }

        /// <summary>
        /// Sets the extension of a request file based on the content type or the URI.
        /// </summary>
        public void SetExtension()
        {
            Extension = string.Empty;
            if (_headers.ContentType?.MediaType != null)
                Extension = MimeTypeMap.GetDefaultExtension(_headers.ContentType.MediaType);
            if (Extension == string.Empty)
                Extension = Path.GetExtension(_uri.AbsoluteUri);
        }

        /// <summary>
        /// Generates the filename from the header or the URI.
        /// </summary>
        private void SetFilename()
        {
            FileName = RemoveInvalidFileNameChars(_headers.ContentDisposition?.FileNameStar ?? _headers.ContentDisposition?.FileName ?? string.Empty);
            if (FileName == string.Empty)
            {
                FileName = RemoveInvalidFileNameChars(_uri.Segments.Last() ?? string.Empty);
                if (FileName == string.Empty)
                    FileName = RemoveInvalidFileNameChars(Path.GetFileName(_uri.AbsoluteUri) ?? string.Empty);
                if (FileName == string.Empty)
                    FileName = "requested_download_" + RemoveInvalidFileNameChars(_uri.Host);
                FileName = FileName.Replace("%20", " ");
                FileName = FileName.Length > 80 ? FileName.Remove(80) : FileName;
            }
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
        /// Removes all invalid characters for a filename out of a string.
        /// </summary>
        /// <param name="input">The input filename.</param>
        /// <returns>The cleared filename.</returns>
        public static string RemoveInvalidFileNameChars(string input)
        {
            StringBuilder fileBuilder = new(input);
            foreach (char c in Path.GetInvalidFileNameChars())
                fileBuilder.Replace(c.ToString(), string.Empty);
            return fileBuilder.ToString();
        }
    }
}
