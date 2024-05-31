using DownloadAssistant.Base;
using DownloadAssistant.Requests;
using DownloadAssistant.Utilities;
using Requests.Options;

namespace DownloadAssistant.Options
{
    /// <summary>
    /// A Class to hold the options for a <see cref="GetRequest"/> class and to modify it.
    /// </summary>
    public record GetRequestOptions : WebRequestOptions<string>
    {
        /// <summary>
        /// Gets or sets the path to the directory where the file should be stored.
        /// </summary>
        /// <value>
        /// The directory path.
        /// </value>
        public string DirectoryPath { get; init; } = IOManager.GetDownloadFolderPath() ?? Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);

        /// <summary>
        /// Gets or sets the filename of the file that will be created and written to.
        /// </summary>
        /// <value>
        /// The filename.
        /// </value>
        public string Filename { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the write mode to specify how the file should be written.
        /// </summary>
        /// <value>
        /// The write mode.
        /// </value>
        public WriteMode WriteMode { get; init; }

        /// <summary>
        /// Gets or sets the length of the stream buffer. Default is 1024 (8kb).
        /// </summary>
        /// <value>
        /// The buffer length.
        /// </value>
        public int BufferLength { get; init; } = 1024;

        /// <summary>
        /// Gets or sets the minimum byte length to restart the request and download only partial. Default is 2Mb.
        /// </summary>
        /// <value>
        /// The minimum reload size.
        /// </value>
        public uint MinReloadSize { get; set; } = 1048576 * 2; //2Mb

        /// <summary>
        /// Gets or sets the maximum of bytes that can be downloaded by the <see cref="GetRequest"/> per second.
        /// </summary>
        /// <value>
        /// The maximum bytes per second.
        /// </value>
        public long? MaxBytesPerSecond { get => _maxBytesPerSecond; init => _maxBytesPerSecond = value > 1 ? value : null; }
        private readonly long? _maxBytesPerSecond = null;

        /// <summary>
        /// Gets or sets the minimum content byte of the Request.
        /// </summary>
        /// <value>
        /// The minimum byte.
        /// </value>
        public long? MinByte
        {
            get => _minByte; init
            {
                _minByte = value < 0 ? throw new ArgumentOutOfRangeException(nameof(MinByte)) : value;
            }
        }
        private readonly long? _minByte = null;

        /// <summary>
        /// Gets or sets the maximum content byte of the Request.
        /// </summary>
        /// <value>
        /// The maximum byte.
        /// </value>
        public long? MaxByte
        {
            get => _maxByte; init
            {
                _maxByte = value < 0 ? throw new ArgumentOutOfRangeException(nameof(MaxByte)) : value;
            }
        }
        private readonly long? _maxByte = null;

        /// <summary>
        /// Gets or sets a value indicating whether the server supports this feature.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the server supports head request; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsHeadRequest { get; init; } = true;

        /// <summary>
        /// Gets or sets the progress to watch the <see cref="GetRequest"/>.
        /// </summary>
        /// <value>
        /// The progress.
        /// </value>
        public Progress<float>? Progress { get; init; } = null;

        /// <summary>
        /// Gets or sets the download range of the <see cref="GetRequest"/>. Start cannot be used with LoadMode.Append, it will switch to LoadMode.Create.
        /// </summary>
        /// <value>
        /// The range.
        /// </value>
        public LoadRange Range { get; init; }

        /// <summary>
        /// Gets or sets the notification when Fileinfos are fetched from the server.
        /// </summary>
        /// <value>
        /// The infos fetched notification.
        /// </value>
        public Notify<GetRequest>? InfosFetched { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetRequestOptions"/> class.
        /// </summary>
        public GetRequestOptions() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetRequestOptions"/> class by copying an existing instance.
        /// </summary>
        /// <param name="options">The options to copy.</param>
        protected GetRequestOptions(GetRequestOptions options) : base(options)
        {
            DirectoryPath = options.DirectoryPath;
            Filename = options.Filename;
            WriteMode = options.WriteMode;
            BufferLength = options.BufferLength;
            MaxBytesPerSecond = options.MaxBytesPerSecond;
            MinByte = options.MinByte;
            MaxByte = options.MaxByte;
            SupportsHeadRequest = options.SupportsHeadRequest;
            Progress = options.Progress;
            Range = options.Range;
            InfosFetched = options.InfosFetched;
            MinReloadSize = options.MinReloadSize;
        }
    }

    /// <summary>
    /// File load mode of the <see cref="GetRequest"/>.
    /// </summary>
    public enum WriteMode
    {
        /// <summary>
        /// Overwrites a file if it already exists, or creates a new one.
        /// </summary>
        Overwrite,
        /// <summary>
        /// Always creates a new file with a different filename and writes into it.
        /// </summary>
        CreateNew,
        /// <summary>
        /// Appends content to the existing file. If the file size is larger than expected, 
        /// it truncates the file to zero bytes and starts over.
        /// </summary>
        /// <remarks>
        /// This mode is useful when you want to ensure that the file size does not exceed 
        /// the actual file limit, and you are willing to lose the existing content.
        /// </remarks>
        AppendOrTruncate,
        /// <summary>
        /// Appends content to the end of the existing file or creates a new file if it does not exist.
        /// Throws a <see cref="FileLoadException"/> if the file size exceeds the expected limit.
        /// </summary>
        Append

    }

}
