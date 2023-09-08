using DownloadAssistant.Base;

namespace DownloadAssistant.Options
{
    /// <summary>
    /// A Class to hold the options for a <see cref="LoadRequest"/> object and to modifies it.
    /// </summary>
    public record LoadRequestOptions : WebRequestOptions<string>
    {
        /// <summary>
        /// Filename of the file that will be created and be written to.
        /// </summary>
        public string Filename
        {
            get => _filename;
            set => _filename = IOManager.RemoveInvalidFileNameChars(value);
        }
        private string _filename = string.Empty;

        /// <summary>
        /// Sets the download range of th<see cref="LoadRequest"/> 
        /// Start can not be used with LoadMode.Append
        /// </summary>
        public LoadRange Range { get; init; }

        /// <summary>
        /// Extensions that are not allowed.
        /// </summary>
        public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The maximum of byte that can be downloaded by the <see cref="LoadRequest"/> per second.
        /// </summary>
        public long? MaxBytesPerSecond { get => _maxBytesPerSecond; set => _maxBytesPerSecond = value > 1 ? value : null; }
        private long? _maxBytesPerSecond = null;

        /// <summary>
        /// Set the value to false if the server does not support this feature
        /// </summary>
        public bool SupportsHeadRequest { get; set; } = true;

        /// <summary>
        /// Length of the stream buffer
        /// Default is 1024 (8kb)
        /// </summary>
        public int BufferLength { get; set; } = 1024;

        /// <summary>
        /// File writing mode.
        /// </summary>
        public WriteMode WriteMode { get; set; } = WriteMode.Append;

        /// <summary>
        /// Path to the diriectory where the temp file should be stored.
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
        /// Path to the directory where the file should be stored.
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
        /// Chunks the <see cref="LoadRequest"/> and partial downloads the files.
        /// Min value is has to be 2
        /// <para>(Only if server supports it)</para>
        /// </summary>
        public int Chunks { get; set; }

        /// <summary>
        /// Merges the chunked files on the fly and not at the end.
        /// </summary>
        public bool MergeWhileProgress { get; set; }

        /// <summary>
        /// Assigns the length of the file to download before the download completes.
        /// </summary>
        public bool PreAllocateFileLength { get; set; }

        /// <summary>
        /// Delete temporary files if the <see cref="LoadRequest"/> failed.
        /// </summary>
        public bool DeleteTmpOnFailure { get; set; }

        /// <summary>
        /// Default Constructor of <see cref="LoadRequestOptions"/>.
        /// </summary>
        public LoadRequestOptions() { }

        /// <summary>
        /// Copy constructor of <see cref="LoadRequestOptions"/>
        /// </summary>
        /// <param name="options">Options to copy</param>
        protected LoadRequestOptions(LoadRequestOptions options) : base(options)
        {
            MergeWhileProgress = options.MergeWhileProgress;
            Chunks = options.Chunks;
            Range = options.Range;
            _temporaryPath = options.TempDestination;
            _destinationPath = options.DestinationPath;
            WriteMode = options.WriteMode;
            BufferLength = options.BufferLength;
            MaxBytesPerSecond = options.MaxBytesPerSecond;
            SupportsHeadRequest = options.SupportsHeadRequest;
            ExcludedExtensions = options.ExcludedExtensions;
            PreAllocateFileLength = options.PreAllocateFileLength;
            Filename = options.Filename;
        }

        /// <summary>
        /// Converts a <see cref="LoadRequestOptions"/> to a <see cref="GetRequestOptions"/> object
        /// </summary>
        /// <returns></returns>
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
            WriteMode = WriteMode
        };
    }
}
