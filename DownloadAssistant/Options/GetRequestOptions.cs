using DownloadAssistant.Base;
using DownloadAssistant.Request;
using DownloadAssistant.Utilities;
using Requests.Options;

namespace DownloadAssistant.Options
{
    /// <summary>
    /// A Class to hold the options for a <see cref="GetRequest"/> class and to modify it.
    /// </summary>
    public record GetRequestOptions : WebRequestOptions<GetRequest>
    {
        /// <summary>
        /// Path to the directory where the file sould be stored to.
        /// </summary>
        public string DirectoryPath { get; init; } = IOManager.GetDownloadFolderPath() ?? Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);

        /// <summary>
        /// Filename of the file that will be created and be written to.
        /// </summary>
        public string Filename { get; init; } = string.Empty;

        /// <summary>
        /// WriteMode to specify how the file should be written.
        /// </summary>
        public WriteMode WriteMode { get; init; }

        /// <summary>
        /// Length of the stream buffer
        /// Default is 1024 (8kb)
        /// </summary>
        public int BufferLength { get; init; } = 1024;

        /// <summary>
        /// The maximum of byte that can be downloaded by the <see cref="GetRequest"/> per second.
        /// </summary>
        public long? MaxBytesPerSecond { get => _maxBytesPerSecond; init => _maxBytesPerSecond = value > 1 ? value : null; }
        private readonly long? _maxBytesPerSecond = null;

        /// <summary>
        /// Min content byte of the Request
        /// </summary>
        public long? MinByte
        {
            get => _minByte; init
            {
                _minByte = value < 0 ? throw new ArgumentOutOfRangeException(nameof(_minByte)) : value;
            }
        }
        private readonly long? _minByte = null;

        /// <summary>
        /// Max content byte of the Request
        /// </summary>
        public long? MaxByte
        {
            get => _maxByte; init
            {

                _maxByte = value < 0 ? throw new ArgumentOutOfRangeException(nameof(_maxByte)) : value;
            }
        }
        private readonly long? _maxByte = null;
        /// <summary>
        /// Set the value to false if the server does not support this feature
        /// </summary>
        public bool SupportsHeadRequest { get; init; } = true;

        /// <summary>
        /// Progress to watch the <see cref="GetRequest"/>.
        /// </summary>
        public Progress<float>? Progress { get; init; } = null;

        /// <summary>
        /// Sets the download range of th<see cref="GetRequest"/> 
        /// Start can not be used with LoadMode.Append it will switch to LoadMode.Create
        /// </summary>
        public LoadRange Range { get; init; }

        /// <summary>
        /// Raised when Fileinfos are fetched from the server
        /// </summary>
        public Notify<GetRequest>? InfosFetched { get; init; }

        /// <summary>
        /// Default Constructor of <see cref="GetRequestOptions"/>.
        /// </summary>
        public GetRequestOptions() { }

        /// <summary>
        /// Copy constructor of <see cref="GetRequestOptions"/>
        /// </summary>
        /// <param name="options">Options to copy</param>
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
        }
    }

    /// <summary>
    /// File load mode of the <see cref="GetRequest"/>.
    /// </summary>
    public enum WriteMode
    {
        /// <summary>
        /// Overwrites a file if it already exists or creates a new one.
        /// </summary>
        Create,
        /// <summary>
        /// Creates always a new file with diffrent filename and writes into it.
        /// </summary>
        CreateNew,
        /// <summary>
        /// Append an already existing file or creates a new one.
        /// </summary>
        Append
    }
}
