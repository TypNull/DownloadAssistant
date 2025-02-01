namespace DownloadAssistant.Media
{
    /// <summary>
    /// Categorizes web resources into specific types (e.g., images, videos).
    /// </summary>
    public class ResourceCategorizer
    {
        /// <summary>
        /// Gets a list of image resources found on the website.
        /// </summary>
        public List<WebItem> Images { get; } = new();

        /// <summary>
        /// Gets a list of video resources found on the website.
        /// </summary>
        public List<WebItem> Videos { get; } = new();

        /// <summary>
        /// Gets a list of audio resources found on the website.
        /// </summary>
        public List<WebItem> Audios { get; } = new();

        /// <summary>
        /// Gets a list of hyperlink resources found on the website.
        /// </summary>
        public List<WebItem> Links { get; } = new();

        /// <summary>
        /// Gets a list of script resources (e.g., JavaScript files) found on the website.
        /// </summary>
        public List<WebItem> Scripts { get; } = new();

        /// <summary>
        /// Gets a list of CSS resources found on the website.
        /// </summary>
        public List<WebItem> CSS { get; } = new();

        /// <summary>
        /// Gets a list of resources with unknown or unsupported types.
        /// </summary>
        public List<WebItem> UnknownType { get; } = new();

        /// <summary>
        /// Gets a list of file resources (e.g., documents, binaries) found on the website.
        /// </summary>
        public List<WebItem> Files { get; } = new();

        /// <summary>
        /// Categorizes a list of web resources into specific types based on their MIME type.
        /// </summary>
        /// <param name="resources">The list of resources to categorize.</param>
        public void Categorize(List<WebItem> resources)
        {
            foreach (WebItem item in resources)
            {
                switch (item.Type.Raw.Split('/')[0].ToLower())
                {
                    case "image":
                        Images.Add(item);
                        break;
                    case "video":
                        Videos.Add(item);
                        break;
                    case "audio":
                        Audios.Add(item);
                        break;
                    case "text":
                        if (item.Type.Raw.EndsWith("css"))
                            CSS.Add(item);
                        else
                            UnknownType.Add(item);
                        break;
                    case "application":
                        if (item.Type.Raw == "application/octet-stream")
                            UnknownType.Add(item);
                        else if (item.Type.Raw.EndsWith("javascript"))
                            Scripts.Add(item);
                        else
                            Files.Add(item);
                        break;
                    default:
                        UnknownType.Add(item);
                        break;
                }
            }
        }
    }
}
