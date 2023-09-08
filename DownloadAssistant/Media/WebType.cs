namespace DownloadAssistant.Media
{
    /// <summary>
    /// Type of a <see cref="WebItem"/>
    /// </summary>
    public record WebType
    {
        /// <summary>
        /// Indicates the type of this media
        /// </summary>
        public MediaType MediaType { get; private set; }

        /// <summary>
        /// The extension of this type
        /// </summary>
        public string Extension { get; private set; } = string.Empty;
        /// <summary>
        /// The raw string type
        /// </summary>
        public string Raw { get; private set; }

        /// <summary>
        /// Full type with main and sub part
        /// </summary>
        public string FullType { get; private set; } = string.Empty;

        /// <summary>
        /// Main Contsructor
        /// </summary>
        /// <param name="rawText">Raw text type to parse</param>
        public WebType(string rawText)
        {
            Raw = rawText;
            if (string.IsNullOrWhiteSpace(rawText))
                return;
            Convert();
        }

        /// <summary>
        /// Converts the raw media type in this WebType.
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

        /// <summary> Bool that indicates if this type is a media file.</summary>
        public bool IsMedia => MediaType != MediaType.NoMedia;

        /// <summary>Bool that indicates if this type is a link.</summary>
        public bool IsLink { get; private set; } = false;

        /// <summary>Bool that indicates if this type is an aplication file.</summary>
        public bool IsApplication => MediaType == MediaType.Application;

        /// <summary>Bool that indicates if this type is a font file.</summary>
        public bool IsFont => MediaType == MediaType.Font;

        /// <summary>Bool that indicates if this type is a image file.</summary>
        public bool IsImage => MediaType == MediaType.Image;

        /// <summary>Bool that indicates if this type is a message file.</summary>
        public bool IsMessage => MediaType == MediaType.Message;

        /// <summary> Bool that indicates if this type is a model.</summary>
        public bool IsModel => MediaType == MediaType.Model;

        /// <summary>Bool that indicates if this type file has more media types.</summary>
        public bool IsMultipart => MediaType == MediaType.Multipart;

        /// <summary>Bool that indicates if this type is a text file.</summary>
        public bool IsText => MediaType == MediaType.Text;

        /// <summary>Bool that indicates if this type is a audio file.</summary>
        public bool IsAudio => MediaType == MediaType.Audio;

        /// <summary>Bool that indicates if this type is a video file.</summary>
        public bool IsVideo => MediaType == MediaType.Video;

        /// <summary>Bool that indicates if the <see cref="MediaType"/> is unknown.</summary>
        public bool IsUnknown => MediaType == MediaType.Unknown;
    }
}
