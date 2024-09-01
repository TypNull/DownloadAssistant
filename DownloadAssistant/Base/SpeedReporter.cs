namespace System
{
    /// <summary>
    /// Provides an ISpeedReporter{T} that invokes callbacks for the latest reported speed value.
    /// </summary>
    /// <typeparam name="T">Specifies the type of the speed report value.</typeparam>
    public class SpeedReporter<T> : IProgress<T>
    {
        private readonly SynchronizationContext _synchronizationContext;
        private readonly Action<T>? _handler;
        private readonly SendOrPostCallback _invokeHandlers;
        private T _latestValue = default!;
        private long _lastReportTime;
        private int _isWaiting = 0;
        private int _isReporting = 0;

        /// <summary>
        /// The timeout for the speed reporter in milliseconds.
        /// Note that the actual reporting interval may not be exactly the set timeout due to various factors.
        /// </summary>
        public int Timeout { get => _timeout; set => _timeout = value < 0 ? 0 : value; }
        private int _timeout = 0;

        /// <summary>Initializes the <see cref="SpeedReporter{T}"/> with a default timeout.</summary>
        public SpeedReporter()
        {
            _synchronizationContext = SynchronizationContext.Current ?? SpeedReporterStatics.DefaultContext;
            _invokeHandlers = new SendOrPostCallback(InvokeHandlers);
        }

        /// <summary>Initializes the <see cref="SpeedReporter{T}"/> with the specified callback and timeout.</summary>
        public SpeedReporter(Action<T> handler) : this()
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        /// <summary>Raised for each reported speed value.</summary>
        public event EventHandler<T>? SpeedChanged;

        /// <summary>Reports a speed change.</summary>
        void IProgress<T>.Report(T value) => OnReport(value);

        /// <summary>Reports a speed change.</summary>
        protected virtual void OnReport(T value)
        {
            _latestValue = value;
            if (Interlocked.CompareExchange(ref _isReporting, 1, 0) == 1)
                return;
            long currentTime = Environment.TickCount;
            if (currentTime - _lastReportTime > Timeout - 20)
            {
                _lastReportTime = currentTime;
                Post();
            }
            else if (Interlocked.CompareExchange(ref _isWaiting, 1, 0) == 0)
            {
                _latestValue = value;
                int delay = Timeout - (int)(currentTime - _lastReportTime);
                _lastReportTime = currentTime + delay;
                ScheduleDelayedPost(delay);
            }
            _isReporting = 0;
        }

        private void ScheduleDelayedPost(int delay) => Task.Run(async () =>
        {
            await Task.Delay(delay);
            _lastReportTime = Environment.TickCount;
            _synchronizationContext.Post(_invokeHandlers, _latestValue);
        });

        private void Post() => _synchronizationContext.Post(_invokeHandlers, _latestValue);


        /// <summary>Invokes the action and event callbacks.</summary>
        private void InvokeHandlers(object? state)
        {
            T value = _latestValue;
            _isWaiting = 0;
            _handler?.Invoke(value);
            SpeedChanged?.Invoke(this, value);
        }
    }

    /// <summary>Holds static values for <see cref="SpeedReporter{T}"/>.</summary>
    internal static class SpeedReporterStatics
    {
        /// <summary>A default synchronization context that targets the ThreadPool.</summary>
        internal static readonly SynchronizationContext DefaultContext = new();
    }
}