namespace DownloadAssistant.Media
{
    /// <summary>
    /// Represents the type of a <see cref="WebItem"/>.
    /// </summary>
    public record WebType
    {
        /// <summary>
        /// Gets the media type of this instance.
        /// </summary>
        public MediaType MediaType { get; private set; }

        /// <summary>
        /// Gets the file extension associated with this type.
        /// </summary>
        public string Extension { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the raw string representation of this type.
        /// </summary>
        public string Raw { get; private set; }

        /// <summary>
        /// Gets the full type, including the main and sub parts.
        /// </summary>
        public string FullType { get; private set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebType"/> class.
        /// </summary>
        /// <param name="rawText">The raw text type to parse.</param>
        public WebType(string rawText)
        {
            Raw = rawText;
            if (string.IsNullOrWhiteSpace(rawText))
                return;
            Convert();
        }

        /// <summary>
        /// Converts the raw media type into a <see cref="MediaType"/> and determines the file extension.
        /// </summary>
        private void Convert()
        {
            string[] splitted = Raw.Split('/');
            if (splitted.Length < 1)
                return;
            string mediaType = splitted[0].ToLower().Trim();

            Extension = splitted[1].ToLower().Trim();
            FullType = mediaType + "/" + Extension;
            if (FullType is "text/plain" or "application/octet-stream")
                MediaType = MediaType.Unknown;
            else if (Enum.TryParse(string.Concat(mediaType[0].ToString().ToUpper(), mediaType[1..]), out MediaType typ))
                MediaType = typ;
            else
                MediaType = MediaType.NoMedia;
            if (FullType == "link/url")
                IsLink = true;
        }

        /// <summary>
        /// Gets a value indicating whether this type represents a media file.
        /// </summary>
        public bool IsMedia => MediaType != MediaType.NoMedia;

        /// <summary>
        /// Gets a value indicating whether this type represents a link.
        /// </summary>
        public bool IsLink { get; private set; } = false;

        /// <summary>
        /// Gets a value indicating whether this type represents an application file.
        /// </summary>
        public bool IsApplication => MediaType == MediaType.Application;

        /// <summary>
        /// Gets a value indicating whether this type represents a font file.
        /// </summary>
        public bool IsFont => MediaType == MediaType.Font;

        /// <summary>
        /// Gets a value indicating whether this type represents an image file.
        /// </summary>
        public bool IsImage => MediaType == MediaType.Image;

        /// <summary>
        /// Gets a value indicating whether this type represents a message file.
        /// </summary>
        public bool IsMessage => MediaType == MediaType.Message;

        /// <summary>
        /// Gets a value indicating whether this type represents a model.
        /// </summary>
        public bool IsModel => MediaType == MediaType.Model;

        /// <summary>
        /// Gets a value indicating whether this type represents a multipart file.
        /// </summary>
        public bool IsMultipart => MediaType == MediaType.Multipart;

        /// <summary>
        /// Gets a value indicating whether this type represents a text file.
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
        /// Gets a value indicating whether the <see cref="MediaType"/> is unknown.
        /// </summary>
        public bool IsUnknown => MediaType == MediaType.Unknown;
    }
}
