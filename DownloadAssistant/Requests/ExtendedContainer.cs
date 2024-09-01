using Requests;

namespace DownloadAssistant.Requests
{
    /// <summary>
    /// Combines requests, their progress indicators, and speed reporters.
    /// </summary>
    /// <typeparam name="TRequest">Type of requests to merge.</typeparam>
    public class ExtendedContainer<TRequest> : RequestContainer<TRequest>, IProgressableRequest, ISpeedReportable where TRequest : IRequest
    {
        /// <summary>
        /// Merged progress of all requests.
        /// </summary>
        public Progress<float> Progress => _progress;
        private readonly CombinableProgress _progress = new();

        /// <summary>
        /// Merged speed reporter of all requests.
        /// </summary>
        public SpeedReporter<long> SpeedReporter => _speedReporter;
        private readonly CombinableSpeedReporter _speedReporter = new();

        /// <summary>
        /// Main constructor for <see cref="ExtendedContainer{TRequest}"/>.
        /// </summary>
        public ExtendedContainer() { }

        /// <summary>
        /// Constructor to merge multiple <see cref="IRequest"/> instances.
        /// </summary>
        /// <param name="requests">Requests to merge.</param>
        public ExtendedContainer(params TRequest[] requests) : this() => AddRange(requests);


        /// <summary>
        /// Adds an <see cref="IRequest"/> to the <see cref="ExtendedContainer{TRequest}"/>.
        /// </summary>
        /// <param name="request">The request to add.</param>
        public new void Add(TRequest request)
        {
            base.Add(request);
            AttachProgress(request);
            AttachSpeedReporter(request);
        }

        /// <summary>
        /// Adds a range of <see cref="IRequest"/> instances to the container.
        /// </summary>
        /// <param name="requests">Requests to add.</param>
        public override void AddRange(params TRequest[] requests)
        {
            base.AddRange(requests);
            foreach (var request in requests)
            {
                AttachProgress(request);
                AttachSpeedReporter(request);
            }
        }

        private void AttachProgress(TRequest request)
        {
            if (request is IProgressableRequest progressable && progressable.Progress != null)
                _progress?.Attach(progressable.Progress);
        }

        private void AttachSpeedReporter(TRequest request)
        {
            if (request is ISpeedReportable speedReportable && speedReportable.SpeedReporter != null)
                _speedReporter?.Attach(speedReportable.SpeedReporter);
        }

        /// <summary>
        /// Removes one or more <see cref="IRequest"/> instances from this container.
        /// </summary>
        /// <param name="requests">Requests to remove.</param>
        public override void Remove(params TRequest[] requests)
        {
            base.Remove(requests);
            foreach (var request in requests)
            {
                if (request is IProgressableRequest progressable && progressable.Progress != null)
                    _progress?.TryRemove(progressable.Progress);
                if (request is ISpeedReportable speedReportable && speedReportable.SpeedReporter != null)
                    _speedReporter?.TryRemove(speedReportable.SpeedReporter);
            }
        }


        /// <summary>
        /// Combines different speed reporters into one by summing their values.
        /// </summary>
        private class CombinableSpeedReporter : SpeedReporter<long>
        {
            private readonly List<SpeedReporter<long>> _speedReporters = new();
            private readonly List<long> _values = new();
            private readonly ReaderWriterLockSlim _lock = new();

            /// <summary>
            /// Gets the count of attached <see cref="SpeedReporter{T}"/> instances.
            /// </summary>
            public int Count => _speedReporters.Count;

            /// <summary>
            /// Initializes a new instance of the <see cref="CombinableSpeedReporter"/> class.
            /// </summary>
            public CombinableSpeedReporter() { }

            /// <summary>
            /// Initializes a new instance of the <see cref="CombinableSpeedReporter"/> class with the specified callback.
            /// </summary>
            /// <param name="handler">
            /// A handler to invoke for each reported speed value. This handler will be invoked
            /// in addition to any delegates registered with the SpeedChanged event.
            /// Depending on the SynchronizationContext instance captured by
            /// the SpeedReporter{T} at construction, it's possible that this handler instance
            /// could be invoked concurrently with itself.
            /// </param>
            /// <exception cref="ArgumentNullException">The handler is null.</exception>
            public CombinableSpeedReporter(Action<long> handler) : base(handler) { }

            /// <summary>
            /// Attaches a speed reporter to this CombinableSpeedReporter instance.
            /// </summary>
            /// <param name="speedReporter">The <see cref="SpeedReporter{T}"/></param>
            public void Attach(SpeedReporter<long> speedReporter)
            {
                _lock.EnterWriteLock();
                try
                {
                    _speedReporters.Add(speedReporter);
                    _values.Add(0);
                }
                finally { _lock.ExitWriteLock(); }
                speedReporter.SpeedChanged += OnSpeedChanged;
            }

            /// <summary>
            /// Attempts to remove an attached speed reporter.
            /// </summary>
            /// <param name="speedReporter">The <see cref="SpeedReporter{T}"/> instance to remove.</param>
            /// <returns>True if removal was successful; otherwise, false.</returns>
            public bool TryRemove(SpeedReporter<long> speedReporter)
            {
                _lock.EnterWriteLock();
                try
                {
                    int index = _speedReporters.FindIndex(x => x == speedReporter);
                    if (index == -1)
                        return false;
                    _speedReporters.RemoveAt(index);
                    _values.RemoveAt(index);
                }
                finally { _lock.ExitWriteLock(); }
                speedReporter.SpeedChanged -= OnSpeedChanged;
                return true;
            }

            /// <summary>
            /// Called when any attached speed reporter reports a change in speed.
            /// </summary>
            /// <param name="sender">The sender object.</param>
            /// <param name="value">The speed value.</param>
            private void OnSpeedChanged(object? sender, long value)
            {
                long sum = 0;
                int n = _speedReporters.Count;
                _lock.EnterReadLock();
                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (ReferenceEquals(_speedReporters[i], sender))
                            _values[i] = value;
                        sum += _values[i];
                    }
                }
                finally { _lock.ExitReadLock(); }
                OnReport(sum);
            }
        }

        /// <summary>
        /// Combines different progress trackers into one.
        /// </summary>
        private class CombinableProgress : Progress<float>
        {
            private readonly List<Progress<float>> _progressors = new();
            private readonly List<float> _values = new();
            private readonly ReaderWriterLockSlim _lock = new();

            /// <summary>
            /// Gets the count of attached <see cref="Progress{T}"/> instances.
            /// </summary>
            public int Count => _progressors.Count;

            /// <summary>
            /// Initializes a new instance of the <see cref="CombinableProgress"/> class.
            /// </summary>
            public CombinableProgress() { }

            /// <summary>
            /// Initializes a new instance of the <see cref="CombinableProgress"/> class with the specified callback.
            /// </summary>
            /// <param name="handler">
            /// A handler to invoke for each reported progress value. This handler will be invoked
            /// in addition to any delegates registered with the ProgressChanged event.
            /// Depending on the SynchronizationContext instance captured by
            /// the Progress{T} at construction, it's possible that this handler instance
            /// could be invoked concurrently with itself.
            /// </param>
            /// <exception cref="ArgumentNullException">The handler is null.</exception>
            public CombinableProgress(Action<float> handler) : base(handler) { }

            /// <summary>
            /// Attaches a progress tracker to this CombinableProgress instance.
            /// </summary>
            /// <param name="progress">The <see cref="Progress{T}"/></param>
            public void Attach(Progress<float> progress)
            {
                _lock.EnterWriteLock();
                try
                {
                    _progressors.Add(progress);
                    _values.Add(0);
                }
                finally { _lock.ExitWriteLock(); }
                progress.ProgressChanged += OnProgressChanged;
            }

            /// <summary>
            /// Attempts to remove an attached progress tracker.
            /// </summary>
            /// <param name="progress">The <see cref="Progress{T}"/> instance to remove.</param>
            /// <returns>True if removal was successful; otherwise, false.</returns>
            public bool TryRemove(Progress<float> progress)
            {
                _lock.EnterWriteLock();
                try
                {
                    int index = _progressors.FindIndex(x => x == progress);
                    if (index == -1)
                        return false;
                    _progressors.RemoveAt(index);
                    _values.RemoveAt(index);
                }
                finally { _lock.ExitWriteLock(); }
                progress.ProgressChanged -= OnProgressChanged;
                return true;
            }

            /// <summary>
            /// Called when any attached progress tracker reports a change in progress.
            /// </summary>
            /// <param name="sender">The sender object.</param>
            /// <param name="e">The progress value.</param>
            private void OnProgressChanged(object? sender, float e)
            {
                double average = 0f;
                _lock.EnterReadLock();
                try
                {
                    average = Calculate(sender, e);
                }
                finally { _lock.ExitReadLock(); }
                OnReport((float)average);
            }

            private double Calculate(object? progress, float value)
            {
                double sum = 0;
                int n = _progressors.Count;
                for (int i = 0; i < n; i++)
                {
                    if (ReferenceEquals(_progressors[i], progress))
                        _values[i] = value;
                    sum += _values[i];
                }
                return sum /= n;
            }
        }
    }
}
