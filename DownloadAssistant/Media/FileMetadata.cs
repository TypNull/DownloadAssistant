using Microsoft.Win32;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;

namespace DownloadAssistant.Media
{
    public class FileMetadata
    {
        private HttpContentHeaders _headers;
        private Uri _uri;
        public FileMetadata(HttpContentHeaders headers, Uri uri) { _headers = headers; _uri = uri; }

        public string GetFilename()
        {
            string fileName = _headers.ContentDisposition?.FileNameStar ?? string.Empty;
            if (fileName == string.Empty)
                fileName = RemoveInvalidFileNameChars(_uri.Segments.Last() ?? string.Empty);
            if (fileName == string.Empty)
                fileName = RemoveInvalidFileNameChars(Path.GetFileName(_uri.AbsoluteUri) ?? string.Empty);
            if (fileName == string.Empty)
                fileName = "requested_download_" + RemoveInvalidFileNameChars(_uri.Host);
            return fileName;
        }

        /// <summary>
        /// Builds the guessed Filename from a given url and response
        /// </summary>
        /// <param name="headers">ContentHeaders of the request</param>
        /// <param name="preSetFilename">Name that was pre set</param>
        /// <param name="uri">Given Uri/Url</param>
        /// <returns></returns>
        public string BuildFilename(string preSetFilename)
        {
            string fileName = preSetFilename;
            if (fileName == string.Empty || fileName == "*" || fileName == "*.*")
            {
                fileName = GetFilename();
                fileName = fileName.Contains('.') ? fileName : fileName + GetExtension();
            }
            else if (fileName.StartsWith("*"))
                fileName = Path.GetFileNameWithoutExtension(GetFilename()) + fileName[1..];
            else if (fileName.EndsWith(".*"))
                fileName = fileName[..^2] + GetExtension();
            return fileName;
        }

        /// <summary>
        /// Gets the Extension of a requerst
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="uri"></param>
        /// <returns></returns>
        public string GetExtension()
        {
            string ext = string.Empty;
            if (_headers.ContentType?.MediaType != null)
                ext = GetDefaultExtension(_headers.ContentType.MediaType);
            if (ext == string.Empty)
                ext = Path.GetExtension(_uri.AbsoluteUri);
            return ext;
        }

        /// <summary>
        /// Removes all invalid Characters for a filename out of a string
        /// </summary>
        /// <param name="name">input filename</param>
        /// <returns>Clreared filename</returns>
        public static string RemoveInvalidFileNameChars(string name)
        {
            StringBuilder fileBuilder = new(name);
            foreach (char c in Path.GetInvalidFileNameChars())
                fileBuilder.Replace(c.ToString(), string.Empty);
            return fileBuilder.ToString();
        }

        /// <summary>
        /// Gets the default extension aof an mimeType
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns>A Extension as string</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="SecurityException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public string GetDefaultExtension(string mimeType)
        {
            string result = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RegistryKey? key;
                object? value;

                key = Registry.ClassesRoot.OpenSubKey(@"MIME\Database\Content Type\" + mimeType, false);
                value = key?.GetValue("Extension", null);
                result = value?.ToString() ?? string.Empty;
            }

            if (result == string.Empty)
                result = MimeTypeMap.GetExtension(mimeType, false);
            return result;
        }
    }
}
