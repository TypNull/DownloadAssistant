using System.Collections;
using System.Globalization;
using System.Resources;

namespace DownloadAssistant.Media
{

    /// <summary>
    /// A static class that maps MIME types to file extensions and vice versa.
    /// </summary>
    public static class MimeTypeMap
    {
        private const string DefaultMimeType = "application/octet-stream";
        private static readonly Lazy<IDictionary<string, string>> _mappings = new(BuildMappings);

        /// <summary>
        /// Gets the default file extension for a given MIME type.
        /// </summary>
        /// <param name="mimeType">The MIME type to get the extension for.</param>
        /// <returns>The default file extension for the given MIME type.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mimeType"/> is null.</exception>
        public static string GetDefaultExtension(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
                throw new ArgumentNullException(nameof(mimeType));

            // Use the mappings dictionary to find the extension
            return GetExtension(mimeType, throwErrorIfNotFound: false);
        }

        /// <summary>
        /// Builds the mappings between MIME types and file extensions.
        /// </summary>
        /// <returns>A dictionary of MIME types and their corresponding file extensions.</returns>
        private static IDictionary<string, string> BuildMappings
        {
            get
            {
                ResourceManager resourceManager = new("DownloadAssistant.Media.MimeTypes", typeof(MimeTypeMap).Assembly);
                ResourceSet? resourceSet = resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);


                Dictionary<string, string> mappings = new(StringComparer.OrdinalIgnoreCase);
                if (resourceSet == null)
                    return mappings;

                foreach (DictionaryEntry entry in resourceSet)
                    if (entry.Key is string key && entry.Value is string value)
                        mappings[key] = value;

                foreach (KeyValuePair<string, string> mapping in mappings.GroupBy(kvp => kvp.Value).ToDictionary(group => group.Key, group => group.First().Key))
                    mappings[mapping.Key] = mapping.Value;

                return mappings;
            }
        }

        /// <summary>
        /// Tries to get the MIME type for a given file name or extension.
        /// </summary>
        /// <param name="str">The file name or extension to get the MIME type for.</param>
        /// <param name="mimeType">When this method returns, contains the MIME type, if found; otherwise, null.</param>
        /// <returns><c>true</c> if a MIME type was found; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="str"/> is null.</exception>
        public static bool TryGetMimeType(string str, out string mimeType)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            str = RemoveQueryString(str);

            if (!str.StartsWith('.'))
                str = ExtractExtension(str);
            return _mappings.Value.TryGetValue(str, out mimeType!);
        }

        /// <summary>
        /// Gets the MIME type for a given file name or extension.
        /// </summary>
        /// <param name="str">The file name or extension to get the MIME type for.</param>
        /// <returns>The MIME type, if found; otherwise, "application/octet-stream".</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="str"/> is null.</exception>
        public static string GetMimeType(string str) => TryGetMimeType(str, out string result) ? result : DefaultMimeType;


        /// <summary>
        /// Gets the file extension for a given MIME type.
        /// </summary>
        /// <param name="mimeType">The MIME type to get the extension for.</param>
        /// <param name="throwErrorIfNotFound">If set to <c>true</c>, throws an exception if the extension is not found.</param>
        /// <returns>The file extension for the given MIME type, or an empty string if the extension is not found and <paramref name="throwErrorIfNotFound"/> is set to <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mimeType"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="mimeType"/> starts with a dot or when the MIME type is not registered in the mappings and <paramref name="throwErrorIfNotFound"/> is set to <c>true</c>.</exception>
        public static string GetExtension(string mimeType, bool throwErrorIfNotFound = true)
        {
            if (string.IsNullOrEmpty(mimeType))
                throw new ArgumentNullException(nameof(mimeType));

            if (mimeType.StartsWith('.'))
                throw new ArgumentException("Requested MIME type is not valid: " + mimeType);

            if (_mappings.Value.TryGetValue(mimeType, out string? extension))
                return extension;

            if (throwErrorIfNotFound)
                throw new ArgumentException("Requested MIME type is not registered: " + mimeType);

            return string.Empty;
        }

        /// <summary>
        /// Removes the query string from a file name or URL.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The input string without the query string.</returns>
        private static string RemoveQueryString(string input)
        {
            int queryIndex = input.IndexOf('?');
            return queryIndex == -1 ? input : input[..queryIndex];
        }

        /// <summary>
        /// Extracts the file extension from a file name or path.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The file extension, prefixed with a dot.</returns>
        private static string ExtractExtension(string input)
        {
            int lastDotIndex = input.LastIndexOf('.');
            return lastDotIndex == -1 ? string.Empty : input[lastDotIndex..];
        }
    }
}