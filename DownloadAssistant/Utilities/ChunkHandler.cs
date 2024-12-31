using DownloadAssistant.Base;
using DownloadAssistant.Requests;

namespace DownloadAssistant.Utilities
{
    /// <summary>
    /// Handles and merges the chunks of a file download.
    /// </summary>
    public class ChunkHandler
    {
        private GetRequest? _reportetRequest;

        /// <summary>
        /// Container for all chunks of the file download.
        /// </summary>
        public ExtendedContainer<GetRequest> RequestContainer { get; } = new();

        /// <summary>
        /// The number of bytes that have been written to the temporary file.
        /// </summary>
        public long BytesWritten { get; private set; } = 0;

        /// <summary>
        /// The total number of bytes that have been written to all chunk files.
        /// </summary>
        public long BytesDownloaded => Requests.Sum(x => x.BytesWritten);

        /// <summary>
        /// Gets all the requests that are in the <see cref="RequestContainer"/>.
        /// </summary>
        public GetRequest[] Requests => RequestContainer.ToArray();

        private int[] _stateArray = Array.Empty<int>();

        private bool _infoFetched = false;

        private int _isCopying = 0;

        /// <summary>
        /// Gets a value indicating whether all requests in the <see cref="ChunkHandler"/> have been completed.
        /// </summary>
        public bool AllCompleted => _stateArray.All(state => state > 0);

        /// <summary>
        /// Merges all chunked parts of a file into one large file.
        /// </summary>
        /// <param name="destination">The path to the destination file.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether all chunks were successfully merged.</returns>
        public async Task<bool> StartMergeTo(string destination)
        {
            if (Interlocked.CompareExchange(ref _isCopying, 1, 0) == 1) return false;

            if (_stateArray.Length == 0 || _stateArray[0] < 1)
            {
                _isCopying = 0;
                return false;
            }

            await MergeChunks(destination);

            Interlocked.CompareExchange(ref _isCopying, 0, 1);
            return AllCompleted && (_stateArray.All(x => x == 2) || await StartMergeTo(destination));

        }

        /// <summary>
        /// Merges the chunks of data into the specified destination.
        /// </summary>
        /// <param name="destination">The destination file path where the chunks will be merged.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task MergeChunks(string destination)
        {
            FileStream? outputStream = null;
            try
            {
                outputStream = new(destination, FileMode.Append);
                for (int i = 0; i < Requests.Length; i++)
                {
                    int state = _stateArray[i];
                    if (state < 1)
                        break;
                    if (state > 1)
                        continue;

                    string path = Requests[i].FilePath;
                    if (!File.Exists(path))
                        break;

                    await WriteChunkToDestination(path, outputStream);
                    Interlocked.Exchange(ref _stateArray[i], 2);
                }
            }
            catch (Exception) { }
            finally
            {
                if (outputStream != null)
                {
                    await outputStream.FlushAsync();
                    await outputStream.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// Writes a chunk of data from the specified path to the output stream.
        /// </summary>
        /// <param name="path">The path of the chunk to be written.</param>
        /// <param name="outputStream">The output stream where the chunk will be written.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task WriteChunkToDestination(string path, FileStream outputStream)
        {
            FileStream inputStream = File.OpenRead(path);
            await inputStream.CopyToAsync(outputStream);
            BytesWritten += inputStream.Length;
            await inputStream.FlushAsync();
            await inputStream.DisposeAsync();
            File.Delete(path);
        }

        /// <summary>
        /// Attempts to set the number of written bytes if no bytes have been downloaded yet.
        /// </summary>
        /// <param name="bytes">The number of downloaded bytes.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether the operation was successful.</returns>
        public async Task<bool> TrySetBytesAsync(long bytes)
        {
            if (BytesWritten != 0 || bytes == 0) return false;

            (int count, bool hasRest) = CalculatePartialContentLength(bytes);

            ProcessRequestsCompletion(count);

            BytesWritten = bytes;

            if (hasRest)
                PauseAndReplaceRequest(count, bytes);

            await DeleteChunkFiles(count);

            return true;
        }

        /// <summary>
        /// Calculates the partial content length based on the number of bytes.
        /// </summary>
        /// <param name="bytes">The number of bytes to calculate the partial content length for.</param>
        /// <returns>A tuple containing the count of requests and the remaining bytes.</returns>
        private (int count, bool rest) CalculatePartialContentLength(long bytes)
        {
            long rest = bytes;
            int count = 0;
            while (true)
            {
                long? partial = RequestContainer[count].PartialContentLength;
                if (partial == null)
                    LoadRange.ToAbsolut(RequestContainer[count].StartOptions.Range, _reportetRequest!.FullContentLength!.Value, out partial);
                if (rest < partial)
                    break;
                count++;
                rest -= partial!.Value;
            }
            return (count, rest != 0);
        }

        /// <summary>
        /// Processes the requests up to the given count.
        /// </summary>
        /// <param name="count">The number of requests to process.</param>
        private void ProcessRequestsCompletion(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Interlocked.Exchange(ref _stateArray[i], 2);
                _ = RequestContainer[i].RunToCompleatedAsync();
            }
        }

        /// <summary>
        /// Marks the specified request chunk as completed and ready to be merged.
        /// </summary>
        /// <param name="request">The request chunk to mark as completed.</param>
        public void MarkChunkAsCompleted(GetRequest request)
        {
            int index = Array.IndexOf(Requests, request);
            if (index >= 0)
                Interlocked.CompareExchange(ref _stateArray[index], 1, 0);
        }

        /// <summary>
        /// Pauses and replaces the request at the given index with a new request.
        /// </summary>
        /// <param name="count">The index of the request to replace.</param>
        /// <param name="bytes">The number of bytes for the new request.</param>
        private void PauseAndReplaceRequest(int count, long bytes)
        {
            GetRequest request = RequestContainer[count];
            request.Pause();
            RequestContainer[count] = new GetRequest(RequestContainer[count].Url, RequestContainer[count].StartOptions with
            {
                MinByte = bytes,
                WriteMode = Options.WriteMode.Overwrite,
            });
            request.Dispose();
        }

        /// <summary>
        /// Deletes the files associated with the requests up to the given count.
        /// </summary>
        /// <param name="count">The number of requests whose files should be deleted.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task DeleteChunkFiles(int count)
        {
            for (int i = 0; i < count; i++)
            {
                await RequestContainer[i].Task;
                try
                {
                    if (File.Exists(RequestContainer[i].FilePath))
                        File.Delete(RequestContainer[i].FilePath);
                }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// Adds a <see cref="GetRequest"/> that represents a chunk to the RequestContainer.
        /// </summary>
        /// <param name="request">The <see cref="GetRequest"/> that represents a chunked part of the data.</param>
        public void Add(GetRequest request)
        {
            RequestContainer.Add(request);
            if (_stateArray.Length < Requests.Length)
                Array.Resize(ref _stateArray, Requests.Length);
        }

        /// <summary>
        /// Sets the ContentLength for all chunks based on a specified <see cref="GetRequest"/>.
        /// </summary>
        /// <param name="requestValue">The <see cref="GetRequest"/> used to set the ContentLength for all chunks.</param>
        public void SetInfos(GetRequest requestValue)
        {
            if (_infoFetched) return;
            _infoFetched = true;
            _reportetRequest = requestValue;
            if (requestValue.FullContentLength.HasValue)
                foreach (GetRequest request in Requests)
                    request.SetContentLength(requestValue.FullContentLength.Value);
        }
    }
}