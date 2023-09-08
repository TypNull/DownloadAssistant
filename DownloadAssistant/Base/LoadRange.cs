namespace DownloadAssistant.Base
{
    /// <summary>
    /// Sets and Gets the download range of a file if supported
    /// </summary>
    public readonly struct LoadRange
    {
        /// <summary>
        /// Creates a Range out two longs with absolut values.
        /// </summary>
        /// <param name="start">Start index</param>
        /// <param name="end">End index</param>
        public LoadRange(long? start, long? end)
        {
            ThrowWhenLessThanNull(start, end);
            if (start >= end)
                throw new InvalidOperationException($"{nameof(start)} has to be less than {nameof(end)}");
            Start = NullConvert(start, 0);
            End = end;
            Length = 1 + End - (Start ?? 0);
        }

        private static long? NullConvert(long? value, long valueEqualsNull) => value == valueEqualsNull ? null : value;

        /// <summary>
        /// Creates a LoadRange object with a reative length
        /// </summary>
        /// <param name="part">Is the Index of the part that should load</param>
        /// <param name="length">Number of parts that are awailable</param>
        /// <exception cref="InvalidOperationException"><paramref name="part"/> can not be larger or the same value as <paramref name="length"/></exception>
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
        /// Creates a LoadRange object with a reative length as promille 
        /// </summary>
        /// <param name="start">Start is the start promille value between 0 and 1</param>
        /// <param name="end">End ist the last promille value between 0 and 1</param>
        /// <exception cref="InvalidOperationException"><paramref name="start"/> can not be larger or the same value as <paramref name="end"/></exception>
        /// <exception cref="InvalidOperationException"><paramref name="start"/> and <paramref name="end"/> need to have a value between 0 and 1 </exception>
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
        /// If the LongRange object is emty
        /// </summary>
        /// <returns>A bool that indicates a filled range</returns>
        public bool IsEmty => Start == null && End == null && Length == null;


        /// <summary>
        /// Retuns the Length
        /// </summary>
        public long? Length { get; }

        /// <summary>
        /// Start point in bytes
        /// zero based
        /// </summary>
        public long? Start { get; }
        /// <summary>
        /// End point in bytes
        /// zero based
        /// </summary>
        public long? End { get; }

        /// <summary>
        /// If the values are relativ
        /// </summary>
        public bool IsAbsolut { get; } = false;

        /// <summary>
        /// If the values are promille values between 0-1000
        /// </summary>
        public bool IsPromille { get; } = false;
    }
}
