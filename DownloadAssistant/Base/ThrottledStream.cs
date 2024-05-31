namespace DownloadAssistant.Base
{
    /// <summary>
    /// A Stream implementation that throttles the process
    /// </summary>
    public class ThrottledStream : Stream
    {
        /// <summary>
        /// A constant used to specify an infinite number of bytes that can be transferred per second.
        /// </summary>
        public const long Infinite = 0;

        /// <summary>
        /// The base stream.
        /// </summary>
        private readonly Stream _baseStream;

        /// <summary>
        /// The maximum bytes per second that can be transferred through the base stream.
        /// </summary>
        private long _maximumBytesPerSecond;

        /// <summary>
        /// The number of bytes that has been transferred since the last throttle.
        /// </summary>
        private long _byteCount;

        /// <summary>
        /// The start time in milliseconds of the last throttle.
        /// </summary>
        private long _start;

        /// <summary>
        /// Gets the current milliseconds.
        /// </summary>
        /// <value>The current milliseconds.</value>
        protected static long CurrentMilliseconds => Environment.TickCount;

        /// <summary>
        /// Gets or sets the maximum bytes per second that can be transferred through the base stream.
        /// </summary>
        /// <value>The maximum bytes per second.</value>
        public long MaximumBytesPerSecond
        {
            get => _maximumBytesPerSecond;
            set
            {
                if (MaximumBytesPerSecond != value && value >= 0)
                {
                    _maximumBytesPerSecond = value;
                    Reset();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <returns>true if the stream supports reading; otherwise, false.</returns>
        public override bool CanRead => _baseStream.CanRead;

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <value></value>
        /// <returns>true if the stream supports seeking; otherwise, false.</returns>
        public override bool CanSeek => _baseStream.CanSeek;

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <value></value>
        /// <returns>true if the stream supports writing; otherwise, false.</returns>
        public override bool CanWrite => _baseStream.CanWrite;

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        /// <value></value>
        /// <returns>A long value representing the length of the stream in bytes.</returns>
        /// <exception cref="NotSupportedException">The base stream does not support seeking. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Length => _baseStream.Length;

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        /// <value></value>
        /// <returns>The current position within the stream.</returns>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="NotSupportedException">The base stream does not support seeking. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="T:ThrottledStream"/> class with an
        /// infinite amount of bytes that can be processed.
        /// </summary>
        /// <param name="baseStream">The base stream.</param>
        public ThrottledStream(Stream baseStream) : this(baseStream, Infinite) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:ThrottledStream"/> class.
        /// </summary>
        /// <param name="baseStream">The base stream.</param>
        /// <param name="maximumBytesPerSecond">The maximum bytes per second that can be transferred through the base stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when <see cref="Stream"/> is a null reference.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="long"/> is a negative value.</exception>
        public ThrottledStream(Stream baseStream, long maximumBytesPerSecond)
        {
            if (maximumBytesPerSecond < 0)
                maximumBytesPerSecond = Infinite;

            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _maximumBytesPerSecond = maximumBytesPerSecond;
            _start = CurrentMilliseconds;
            _byteCount = 0;
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        public override void Flush() => _baseStream.Flush();

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        public new Task FlushAsync() => _baseStream.FlushAsync();

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        public override Task FlushAsync(CancellationToken cancellationToken) => _baseStream.FlushAsync(cancellationToken);


        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="ArgumentException">The sum of offset and count is larger than the buffer length. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="NotSupportedException">The base stream does not support reading. </exception>
        /// <exception cref="ArgumentNullException">buffer is null. </exception>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrottleAsync(count).Wait();

            return _baseStream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <param name="cancellationToken">CancellationToken to cancel the process.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="ArgumentException">The sum of offset and count is larger than the buffer length. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="NotSupportedException">The base stream does not support reading. </exception>
        /// <exception cref="ArgumentNullException">buffer is null. </exception>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            await ThrottleAsync(count);

            return await _baseStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
        /// <param name="cancellationToken">CancellationToken to cancel the process.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="ArgumentException">The sum of offset and count is larger than the buffer length. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="NotSupportedException">The base stream does not support reading. </exception>
        /// <exception cref="ArgumentNullException">buffer is null. </exception>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await ThrottleAsync(buffer.Length);

            return await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the origin parameter.</param>
        /// <param name="origin">A value of type <see cref="SeekOrigin"></see> indicating the reference point used to obtain the new position.</param>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="NotSupportedException">The base stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="NotSupportedException">The base stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override void SetLength(long value) => _baseStream.SetLength(value);

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="NotSupportedException">The base stream does not support writing. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="ArgumentNullException">buffer is null. </exception>
        /// <exception cref="ArgumentException">The sum of offset and count is greater than the buffer length. </exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrottleAsync(count).Wait();

            _baseStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <param name="cancellationToken">CancellationToken to cancel the process</param>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="NotSupportedException">The base stream does not support writing. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="ArgumentNullException">buffer is null.</exception>
        /// <exception cref="ArgumentException">The sum of offset and count is greater than the buffer length. </exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            await ThrottleAsync(count);

            await _baseStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">A ReadOnlyMemory of bytes. This method copies count bytes from buffer to the current stream.</param>
        /// <param name="cancellationToken">CancellationToken to cancel the process</param>
        /// <exception cref="IOException">An I/O error occurs. </exception>
        /// <exception cref="NotSupportedException">The base stream does not support writing. </exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="ArgumentNullException">buffer is null.</exception>
        /// <exception cref="ArgumentException">The sum of offset and count is greater than the buffer length. </exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await ThrottleAsync(buffer.Length);

            await _baseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a <see cref="string"/> that represents the current <see cref="object"/>
        /// </summary>
        /// <returns>
        /// A <see cref="string"/> that represents the current <see cref="object"/>.
        /// </returns>
        public override string ToString() => _baseStream.ToString() ?? string.Empty;


        /// <summary>
        /// Throttles for the specified buffer size in bytes.
        /// </summary>
        /// <param name="bufferSizeInBytes">The buffer size in bytes.</param>
        protected async Task ThrottleAsync(int bufferSizeInBytes)
        {
            if (_maximumBytesPerSecond <= 0 || bufferSizeInBytes <= 0)
                return;

            _byteCount += bufferSizeInBytes;
            long elapsedMilliseconds = CurrentMilliseconds - _start;

            if (elapsedMilliseconds > 0)
            {
                long bps = _byteCount * 1000L / elapsedMilliseconds;

                if (bps > _maximumBytesPerSecond)
                {
                    long wakeElapsed = _byteCount * 1000L / _maximumBytesPerSecond;
                    int toSleep = (int)(wakeElapsed - elapsedMilliseconds);

                    if (toSleep > 1)
                    {
                        try
                        {
                            await Task.Delay(toSleep);
                        }
                        catch { }
                        Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Will reset the bytecount to 0 and reset the start time to the current time.
        /// </summary>
        protected void Reset()
        {
            if ((CurrentMilliseconds - _start) > 1000)
            {
                _byteCount = 0;
                _start = CurrentMilliseconds;
            }
        }
    }
}
