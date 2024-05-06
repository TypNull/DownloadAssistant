namespace DownloadAssistant.Base
{
    /// <summary>
    /// Represents a range of data to be loaded. This could be an absolute range (in bytes), a relative range (in parts), or a promille range.
    /// </summary>
    public readonly struct LoadRange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoadRange"/> struct with absolute start and end values.
        /// </summary>
        /// <param name="start">The start index of the range.</param>
        /// <param name="end">The end index of the range.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="start"/> is greater than or equal to <paramref name="end"/>.</exception>
        public LoadRange(long? start, long? end)
        {
            ThrowWhenLessThanNull(start, end);
            if (start >= end)
                throw new InvalidOperationException($"{nameof(start)} has to be less than {nameof(end)}");
            Start = NullConvert(start, 0);
            End = end;
            Length = 1 + End - (Start ?? 0);
            IsAbsolut = true;
        }

        private static long? NullConvert(long? value, long valueEqualsNull) => value == valueEqualsNull ? null : value;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadRange"/> struct with a relative range based on the part and total length.
        /// </summary>
        /// <param name="part">The index of the part to load.</param>
        /// <param name="length">The total number of parts available.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="part"/> is greater than or equal to <paramref name="length"/>.</exception>
        public LoadRange(Index part, int length)
        {
            int start = part.IsFromEnd ? part.Value - length : part.Value;
            if (start >= length)
                throw new InvalidOperationException($"{nameof(start)} can not be larger or the same value as {nameof(length)}");
            ThrowWhenLessThanNull(start, length);
            Start = start;
            End = start + 1;
            Length = length;
            IsAbsolut = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadRange"/> struct with a promille range.
        /// </summary>
        /// <param name="start">The start value of the range, as a promille value between 0 and 1.</param>
        /// <param name="end">The end value of the range, as a promille value between 0 and 1.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="start"/> is greater than or equal to <paramref name="end"/>, or when either <paramref name="start"/> or <paramref name="end"/> is not between 0 and 1.</exception>
        public LoadRange(Half start, Half? end = null)
        {
            if (start >= end)
                throw new InvalidOperationException($"{nameof(start)} can not be larger or the same value as {nameof(end)}");
            if (end > (Half)1)
                throw new InvalidOperationException($"{nameof(start)} and {nameof(end)} need to have a value between 0 and 1");
            ThrowWhenLessThanNull((long?)start, (long?)end);

            Start = NullConvert((int)(Half)((double)start * 1000), 0);
            End = NullConvert((int?)(Half?)((double?)end * 1000), 1000);
            Length = end == null ? null : 1000;
            IsAbsolut = false;
            IsPromille = true;
        }

        private static void ThrowWhenLessThanNull(params long?[] numbers)
        {
            if (numbers.Any(x => x < 0))
                throw new InvalidOperationException($"Parameter can not be less than 0");
        }

        /// <summary>
        /// Converts a relative <see cref="LoadRange"/> to an absolute range, based on the given total length.
        /// </summary>
        /// <param name="range">The relative range to convert.</param>
        /// <param name="length">The total length of the data.</param>
        /// <param name="partialLength">Length of the new absolut LoadRange</param>
        /// <returns>A new <see cref="LoadRange"/> representing the absolute range.</returns>
        /// <returns></returns>
        public static LoadRange ToAbsolut(LoadRange range, long length, out long? partialLength)
        {
            LoadRange absolutRange = range;
            if (range.IsAbsolut)
            {
                if (range.Length > length)
                    absolutRange = new(range.Start, null);

                if (range.End == null)
                    partialLength = length - range.Start;
                else
                    partialLength = range.Length;
            }
            else if (range.IsPromille)
            {
                decimal onePromill = (decimal)length / 1000;
                partialLength = (long?)(onePromill * (range.End ?? 1000 - range.Start));
                long? startIndex = (long?)(onePromill * range.Start);
                absolutRange = new LoadRange(startIndex == 0 ? startIndex : startIndex + 1, (long?)(onePromill * range.End));
            }
            else
            {
                decimal? partLength = (decimal)length / range.Length!.Value;
                long? startIndex = (long?)(partLength * range.Start);
                absolutRange = new LoadRange(startIndex == 0 ? startIndex : startIndex + 1, (long?)(partLength * range.End));
                partialLength = absolutRange.Length;
            }
            return absolutRange;
        }

        /// <summary>
        /// Indicates whether the <see cref="LoadRange"/> object is empty.
        /// </summary>
        /// <returns>A boolean value that indicates whether the range is empty. Returns true if the Start, End, and Length properties are all null; otherwise, false.</returns>
        public bool IsEmpty => Start == null && End == null && Length == null;

        /// <summary>
        /// Gets the length of the <see cref="LoadRange"/>.
        /// </summary>
        public long? Length { get; }

        /// <summary>
        /// Gets the start point of the <see cref="LoadRange"/> in bytes. This value is zero-based.
        /// </summary>
        public long? Start { get; }

        /// <summary>
        /// Gets the end point of the <see cref="LoadRange"/> in bytes. This value is zero-based.
        /// </summary>
        public long? End { get; }

        /// <summary>
        /// Indicates whether the values of the <see cref="LoadRange"/> are absolute.
        /// </summary>
        public bool IsAbsolut { get; }

        /// <summary>
        /// Indicates whether the values of the <see cref="LoadRange"/> are promille values between 0 and 1000.
        /// </summary>
        public bool IsPromille { get; } = false;
    }
}
