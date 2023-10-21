using System.Net.Http.Headers;
using System.Text;

namespace DownloadAssistant.Media
{
    /// <summary>
    /// Class to build a file
    /// </summary>
    public class FileMetadata
    {
        private readonly HttpContentHeaders _headers;
        private readonly Uri _uri;

        /// <summary>
        /// Name of the file
        /// </summary>
        public string FileName { get; private set; } = string.Empty;
        /// <summary>
        /// Extension of the file
        /// </summary>
        public string Extension { get; private set; } = string.Empty;
        /// <summary>
        /// Contructor needs
        /// </summary>
        /// <param name="headers">Http headers of the response</param>
        /// <param name="uri">The Url to the file</param>
        public FileMetadata(HttpContentHeaders headers, Uri uri)
        {
            _headers = headers;
            _uri = uri;
            SetFilename();
            SetExtension();
        }

        /// <summary>
        /// Set the extension of a request file
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
        /// Generates the Filename from the header
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
                FileName = FileName.Length > 80 ? FileName.Remove(80) : FileName;
            }
        }

        /// <summary>
        /// Builds the setted Filename
        /// </summary>
        /// <param name="preSetFilename">Name that was pre set</param>
        /// <returns></returns>
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
                if (fileName.StartsWith("*"))
                    fileName = Path.GetFileNameWithoutExtension(FileName) + fileName[1..];
                if (fileName.Contains(".*"))
                    fileName = fileName.Replace(".*", Extension);
                if (fileName.Contains('*'))
                    fileName = fileName.Remove('*');
            }
            return fileName;
        }



        /// <summary>
        /// Removes all invalid Characters for a filename out of a string
        /// </summary>
        /// <param name="input">input filename</param>
        /// <returns>Clreared filename</returns>
        public static string RemoveInvalidFileNameChars(string input)
        {
            StringBuilder fileBuilder = new(input);
            foreach (char c in Path.GetInvalidFileNameChars())
                fileBuilder.Replace(c.ToString(), string.Empty);
            return fileBuilder.ToString();
        }


    }
}
