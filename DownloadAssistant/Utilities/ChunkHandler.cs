using DownloadAssistant.Request;
using Requests;
using Requests.Options;

namespace DownloadAssistant.Utilities
{
    /// <summary>
    /// Handels and merges the chunks
    /// </summary>
    public class ChunkHandler
    {
        /// <summary>
        /// Container for all chunks
        /// </summary>
        public ProgressableContainer<GetRequest> RequestContainer { get; } = new();

        /// <summary>
        /// Bytes that are written to the temp file
        /// </summary>
        public long BytesWritten { get; private set; } = 0;

        /// <summary>
        /// Bytes that are written all chunk files
        /// </summary>
        public long BytesDownloaded => Requests.Sum(x => x.BytesWritten);

        /// <summary>
        /// Gets all Requests that are in the <see cref="RequestContainer"/>
        /// </summary>
        public IReadOnlyList<GetRequest> Requests => RequestContainer.GetRequests();
        private readonly List<GetRequest> _copied = new();

        private bool _infoFetched = false;

        private int _isCopying = 0;
        /// <summary>
        /// Merges all chunked parts of a file into one big file.
        /// </summary>
        /// <returns>A awaitable Task</returns>
        public async Task<bool> StartMergeTo(string destination)
        {
            bool allMerged = false;
            //Return % of merging
            if (Interlocked.CompareExchange(ref _isCopying, 1, 0) == 1)
                return allMerged;
            //Check if the fist part was downloaded
            if (Requests[0].State != RequestState.Compleated)
            {
                _isCopying = 0;
                return allMerged;
            }

            await MergeChunks(destination);

            Interlocked.CompareExchange(ref _isCopying, 0, 1);
            if (Requests.All((s) => s.State == RequestState.Compleated))
            {
                if (Requests.Count == _copied.Count)
                    allMerged = true;
                else allMerged = await StartMergeTo(destination);
            }
            return allMerged;
        }

        private async Task MergeChunks(string destination)
        {
            //FileStream to merge the chunked files
            FileStream? outputStream = null;
            try
            {
                outputStream = new(destination, FileMode.Append);
                for (int i = 0; i < Requests.Count; i++)
                {
                    if (Requests[i].State != RequestState.Compleated)
                        break;
                    if (_copied.Contains(Requests[i]))
                        continue;
                    string path = Requests[i].FilePath;
                    if (!File.Exists(path))
                        break;
                    await WriteChunkToDestination(path, outputStream);
                    _copied.Add(Requests[i]);
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
        /// Add a request that represents a chunk
        /// </summary>
        /// <param name="request">GeRequest that is a chunked part</param>
        public void Add(GetRequest request) => RequestContainer.Add(request);

        /// <summary>
        /// Sets ContentLength to all Chunks based on a <see cref="GetRequest"/>
        /// </summary>
        /// <param name="request"></param>
        public void SetInfos(GetRequest request)
        {
            if (_infoFetched) return;
            _infoFetched = true;
            Requests.ToList().ForEach(x => x.SetContentLength(request.FullContentLegth!.Value));
        }

        /// <summary>
        /// Deletes all files that were created by the chunks
        /// </summary>
        public void DeleteChunks()
        {
            foreach (GetRequest request in Requests)
            {
                try
                {
                    if (File.Exists(request.FilePath))
                        File.Delete(request.FilePath);
                }
                catch (Exception)
                {
                }
            }
        }
    }

}
