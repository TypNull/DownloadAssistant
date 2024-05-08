using DownloadAssistant.Base;
using DownloadAssistant.Requests;
using DownloadAssistant.Utilities;
using Requests.Options;

namespace DownloadAssistant.Options
{
    /// <summary>
    /// A class to hold the options for a <see cref="LoadRequest"/> object and modify it.
    /// </summary>
    public record LoadRequestOptions : WebRequestOptions<string>
    {
        /// <summary>
        /// Gets or sets the filename of the file to be created and written to.
        /// Invalid filename characters are removed.
        /// </summary>
        public string Filename
        {
            get => _filename;
            set => _filename = IOManager.RemoveInvalidFileNameChars(value);
        }
        private string _filename = string.Empty;

        /// <summary>
        /// Gets the download range of the <see cref="LoadRequest"/>. 
        /// Note: Start cannot be used with LoadMode.Append.
        /// </summary>
        public LoadRange Range { get; init; }

        /// <summary>
        /// Event raised when file information is fetched from the server.
        /// </summary>
        public Notify<LoadRequest>? InfosFetched { get; init; }

        /// <summary>
        /// Gets or sets the minimum byte length to restart the request and download only partially. Default is 2Mb.
        /// </summary>
        public uint MinReloadSize { get; set; } = 1048576 * 2; //2Mb

        /// <summary>
        /// Gets or sets the extensions that are not allowed.
        /// </summary>
        public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the maximum number of bytes that can be downloaded by the <see cref="LoadRequest"/> per second.
        /// </summary>
        public long? MaxBytesPerSecond { get => _maxBytesPerSecond; set => _maxBytesPerSecond = value > 1 ? value : null; }
        private long? _maxBytesPerSecond = null;

        /// <summary>
        /// Gets or sets a value indicating whether the server supports the HEAD request. Default is true.
        /// </summary>
        public bool SupportsHeadRequest { get; set; } = true;

        /// <summary>
        /// Gets or sets the length of the stream buffer. Default is 1024 (8kb).
        /// </summary>
        public int BufferLength { get; set; } = 1024;

        /// <summary>
        /// Gets or sets the file writing mode. Default is WriteMode.Append.
        /// </summary>
        public WriteMode WriteMode { get; set; } = WriteMode.Append;

        /// <summary>
        /// Gets or sets the path to the directory where the temporary file should be stored.
        /// Default is the <see cref="DestinationPath"/>.
        /// </summary>
        public string TempDestination
        {
            get => _temporaryPath;
            set
            {
                string path = value;
                if (value != string.Empty && !IOManager.TryGetFullPath(value, out path))
                    throw new ArgumentException("Path is not valid", nameof(TempDestination));

                _temporaryPath = path;
            }

        }
        private string _temporaryPath = string.Empty;

        /// <summary>
        /// Gets or sets the path to the directory where the file should be stored.
        /// </summary>
        public string DestinationPath
        {
            get => _destinationPath;
            set
            {
                if (!IOManager.TryGetFullPath(value, out string path))
                    throw new ArgumentException("Path is not valid", nameof(DestinationPath));
                _destinationPath = path;
            }
        }
        private string _destinationPath = IOManager.GetDownloadFolderPath() ?? Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);

        /// <summary>
        /// Gets or sets the number of chunks for the <see cref="LoadRequest"/> to partially download the files.
        /// Note: Minimum value has to be 2. Only applicable if the server supports it.
        /// </summary>
        public int Chunks { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to merge the chunked files on the fly and not at the end.
        /// </summary>
        public bool MergeWhileProgress { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to delete temporary files if the <see cref="LoadRequest"/> fails.
        /// </summary>
        public bool DeleteTmpOnFailure { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadRequestOptions"/> class.
        /// </summary>
        public LoadRequestOptions() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadRequestOptions"/> class by copying an existing instance.
        /// </summary>
        /// <param name="options">The <see cref="LoadRequestOptions"/> instance to copy.</param>
        protected LoadRequestOptions(LoadRequestOptions options) : base(options)
        {
            MergeWhileProgress = options.MergeWhileProgress;
            Chunks = options.Chunks;
            Range = options.Range;
            _temporaryPath = options.TempDestination;
            _destinationPath = options.DestinationPath;
            DeleteTmpOnFailure = options.DeleteTmpOnFailure;
            WriteMode = options.WriteMode;
            BufferLength = options.BufferLength;
            MaxBytesPerSecond = options.MaxBytesPerSecond;
            SupportsHeadRequest = options.SupportsHeadRequest;
            InfosFetched = options.InfosFetched;
            _filename = options.Filename;
            ExcludedExtensions = options.ExcludedExtensions;
            MinReloadSize = options.MinReloadSize;
        }

        /// <summary>
        /// Converts a <see cref="LoadRequestOptions"/> instance to a <see cref="GetRequestOptions"/> instance.
        /// </summary>
        /// <returns>A <see cref="GetRequestOptions"/> instance with properties copied from this instance.</returns>
        public GetRequestOptions ToGetRequestOptions() => new()
        {
            Range = Range,
            DirectoryPath = TempDestination,
            Filename = Filename,
            AutoStart = AutoStart,
            BufferLength = BufferLength,
            CancellationToken = CancellationToken,
            DelayBetweenAttemps = DelayBetweenAttemps,
            DeployDelay = DeployDelay,
            Handler = Handler,
            MaxBytesPerSecond = MaxBytesPerSecond,
            NumberOfAttempts = NumberOfAttempts,
            SupportsHeadRequest = SupportsHeadRequest,
            UserAgent = UserAgent,
            Priority = Priority,
            Headers = Headers,
            Timeout = Timeout,
            WriteMode = WriteMode,
            MinReloadSize = MinReloadSize
        };
    }
}
