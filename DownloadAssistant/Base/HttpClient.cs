using Requests;
using System.Net;

namespace DownloadAssistant.Base
{
    public partial class HttpGet
    {
        /// <summary>
        /// Main Instance of <see cref="System.Net.Http.HttpClient"/> that will be used to handle HttpRequests the <see cref="IRequest"/> that are using it.
        /// </summary>
        public static HttpClient HttpClient
        {
            get
            {
                if (_initialized)
                    return _httpClient;
                _initialized = true;
                SocketsHttpHandler socketsHandler = new()
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    MaxConnectionsPerServer = 10
                };

                _httpClient = new(socketsHandler);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                _httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
                return _httpClient;
            }
            set => _httpClient = value;
        }

        private static HttpClient _httpClient = HttpClient;
        private static bool _initialized;
    }
}
