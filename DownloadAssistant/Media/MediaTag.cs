namespace DownloadAssistant.Media
{

    /// <summary>
    /// Represents core media type categories
    /// </summary>
    public enum MediaType
    {
        /// <summary>
        /// Visual image formats (e.g., JPEG, PNG, GIF)
        /// </summary>
        Image,

        /// <summary>
        /// Video container formats (e.g., MP4, AVI, MKV)
        /// </summary>
        Video,

        /// <summary>
        /// Audio file formats (e.g., MP3, WAV, AAC)
        /// </summary>
        Audio,

        /// <summary>
        /// Text-based formats (e.g., TXT, HTML, CSV)
        /// </summary>
        Text,

        /// <summary>
        /// Executable and binary formats (e.g., EXE, DLL, APK)
        /// </summary>
        Application,

        /// <summary>
        /// Font resource formats (e.g., TTF, OTF, WOFF)
        /// </summary>
        Font,

        /// <summary>
        /// Archive and compression formats (e.g., ZIP, RAR, 7Z)
        /// </summary>
        Archive,

        /// <summary>
        /// Email and messaging formats (e.g., .eml, .msg)
        /// </summary>
        Message,

        /// <summary>
        /// 3D model formats (e.g., .obj, .stl, .fbx)
        /// </summary>
        Model,

        /// <summary>
        /// Multipart document formats (e.g., multipart/form-data)
        /// </summary>
        Multipart,

        /// <summary>
        /// Unknown or unclassified formats
        /// </summary>
        Unknown
    }
}