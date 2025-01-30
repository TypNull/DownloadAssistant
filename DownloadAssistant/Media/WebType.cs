using System.Globalization;

namespace DownloadAssistant.Media
{
    /// <summary>
    /// Represents the type of a <see cref="WebItem"/> with comprehensive media type analysis.
    /// </summary>
    public record WebType
    {
        /// <summary>
        /// Gets the media type classification of this instance.
        /// </summary>
        public MediaType MediaType { get; private set; }

        /// <summary>
        /// Gets the standard file extension associated with this media type.
        /// </summary>
        public string Extension { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the raw MIME type string representation.
        /// </summary>
        public string Raw { get; private set; }

        /// <summary>
        /// Gets the complete MIME type including main and sub type parts.
        /// </summary>
        public string FullType { get; private set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebType"/> class.
        /// </summary>
        /// <param name="rawText">The raw MIME type string to parse.</param>
        public WebType(string rawText)
        {
            Raw = rawText;
            if (string.IsNullOrWhiteSpace(rawText))
                return;
            Convert();
        }

        /// <summary>
        /// Converts the raw MIME type into a structured media type representation.
        /// </summary>
        private void Convert()
        {
            string[] splitted = Raw.Split('/');
            if (splitted.Length < 1)
                return;

            string mediaType = splitted[0].ToLower().Trim();
            string subType = splitted.Length > 1 ? splitted[1].ToLower().Trim() : "octet-stream";

            Extension = subType;
            FullType = $"{mediaType}/{subType}";

            MediaType = FullType switch
            {
                "text/plain" or "application/octet-stream" => MediaType.Unknown,
                "application/zip" or "application/x-rar-compressed"
                    or "application/x-7z-compressed" => MediaType.Archive,
                _ when Enum.TryParse(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(mediaType), out MediaType type)
                    => type,
                _ => MediaType.Unknown
            };

            IsLink = FullType == "link/url";
        }

        /// <summary>
        /// Gets a value indicating whether this type represents any media content.
        /// </summary>
        public bool IsMedia => MediaType != MediaType.Unknown;

        /// <summary>
        /// Gets a value indicating whether this type represents non-media content.
        /// </summary>
        public bool IsNoMedia => MediaType == MediaType.Unknown;

        /// <summary>
        /// Gets a value indicating whether this type represents an archive file.
        /// </summary>
        public bool IsArchive => MediaType == MediaType.Archive;

        /// <summary>
        /// Gets a value indicating whether this type represents a hyperlink reference.
        /// </summary>
        public bool IsLink { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this type represents an application file.
        /// </summary>
        public bool IsApplication => MediaType == MediaType.Application;

        /// <summary>
        /// Gets a value indicating whether this type represents a font resource.
        /// </summary>
        public bool IsFont => MediaType == MediaType.Font;

        /// <summary>
        /// Gets a value indicating whether this type represents an image file.
        /// </summary>
        public bool IsImage => MediaType == MediaType.Image;

        /// <summary>
        /// Gets a value indicating whether this type represents a message container.
        /// </summary>
        public bool IsMessage => MediaType == MediaType.Message;

        /// <summary>
        /// Gets a value indicating whether this type represents a 3D model file.
        /// </summary>
        public bool IsModel => MediaType == MediaType.Model;

        /// <summary>
        /// Gets a value indicating whether this type represents a multipart document.
        /// </summary>
        public bool IsMultipart => MediaType == MediaType.Multipart;

        /// <summary>
        /// Gets a value indicating whether this type represents a text document.
        /// </summary>
        public bool IsText => MediaType == MediaType.Text;

        /// <summary>
        /// Gets a value indicating whether this type represents an audio file.
        /// </summary>
        public bool IsAudio => MediaType == MediaType.Audio;

        /// <summary>
        /// Gets a value indicating whether this type represents a video file.
        /// </summary>
        public bool IsVideo => MediaType == MediaType.Video;

        /// <summary>
        /// Gets a value indicating whether the media type is unrecognized.
        /// </summary>
        public bool IsUnknown => MediaType == MediaType.Unknown;
    }
}