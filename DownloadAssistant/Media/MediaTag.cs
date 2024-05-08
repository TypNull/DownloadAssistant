namespace DownloadAssistant.Media
{
    /// <summary>
    /// Enum representing the media type of a web file.
    /// </summary>
    public enum MediaType
    {
        /// <summary>
        /// Represents a non-media file.
        /// </summary>
        NoMedia,

        /// <summary>
        /// Represents video and movie files, such as .mp4 or .avi.
        /// </summary>
        Video,

        /// <summary>
        /// Represents audio and music files, such as .mp3 or .wav.
        /// </summary>
        Audio,

        /// <summary>
        /// Represents binary data that require an application to interpret, such as .exe or .dll.
        /// </summary>
        Application,

        /// <summary>
        /// Represents readable text files, such as .txt or .docx.
        /// </summary>
        Text,

        /// <summary>
        /// Represents graphical data formats, such as .jpg or .png.
        /// </summary>
        Image,

        /// <summary>
        /// Represents font or typeface files, such as .ttf or .otf.
        /// </summary>
        Font,

        /// <summary>
        /// Represents model data used to create 2D or 3D scenes, such as .obj or .fbx.
        /// </summary>
        Model,

        /// <summary>
        /// Represents data with multiple media types, such as .html or .xml.
        /// </summary>
        Multipart,

        /// <summary>
        /// Represents email and messaging formats, such as .eml or .msg.
        /// </summary>
        Message,

        /// <summary>
        /// Represents a media type that is unknown or not recognized.
        /// </summary>
        Unknown,
    }
}