using Requests;
using System.Net;

namespace DownloadAssistant.Base
{
    public partial class HttpGet
    {
        /// <summary>
        /// The primary instance of <see cref="System.Net.Http.HttpClient"/>. 
        /// This instance is used to manage HttpRequests for the <see cref="IRequest"/> that utilize it.
        /// </summary>
        public static HttpClient HttpClient
        {
            get
            {
                // If the HttpClient is already initialized, return the existing instance
                if (_initialized)
                    return _httpClient;

                // Initialize the HttpClient
                _initialized = true;

                // Create a new SocketsHttpHandler with specific settings
                SocketsHttpHandler socketsHandler = new()
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    MaxConnectionsPerServer = 10
                };

                // Assign the handler to the HttpClient
                _httpClient = new(socketsHandler);

                // Set the security protocol types for the ServicePointManager
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                // Set the timeout for the HttpClient
                _httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;

                // Add a user agent header to the HttpClient
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.3");

                // Return the initialized HttpClient
                return _httpClient;
            }
            // Allow the HttpClient to be set externally
            set => _httpClient = value;
        }

        // Private field to hold the HttpClient instance
        private static HttpClient _httpClient = HttpClient;

        // Flag to indicate whether the HttpClient has been initialized
        private static bool _initialized;
    }
}
