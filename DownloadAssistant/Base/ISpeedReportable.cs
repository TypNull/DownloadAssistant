namespace Requests
{
    /// <summary>
    /// Represents an interface for requests that support speed reporting.
    /// </summary>
    public interface ISpeedReportable
    {
        /// <summary>
        /// Gets the SpeedReporter instance associated with the request, which is used to report speed values.
        /// </summary>
        SpeedReporter<long>? SpeedReporter { get; }
    }
}
