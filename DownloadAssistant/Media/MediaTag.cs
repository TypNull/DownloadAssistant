namespace DownloadAssistant.Media
{
    /// <summary>
    /// Media type of a web file
    /// </summary>
    public enum MediaType
    {
        /// <summary>Is not a media</summary>
        NoMedia,
        /// <summary>Video and movie files</summary>
        Video,
        /// <summary>Audio and music files</summary>
        Audio,
        /// <summary>Binary data that require an application</summary>
        Application,
        /// <summary>Readable text file</summary>
        Text,
        /// <summary>Graphical data format</summary>
        Image,
        /// <summary>Font or typeface file</summary>
        Font,
        /// <summary>Model data to create 2d or 3d scenes</summary>
        Model,
        /// <summary>Data with multiple media types</summary>
        Multipart,
        /// <summary>Email and messaging formats</summary>
        Message,
        /// <summary>Media type is unknown</summary>
        Unknown,
    }
}
