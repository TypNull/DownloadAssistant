using Requests;
using System.Net.Security;
using System.Security.Authentication;

namespace DownloadAssistant.Base
{
    public partial class HttpGet
    {
        private static readonly object _lockObject = new();
        private static HttpClient? _httpClient;

        /// <summary>
        /// The primary instance of <see cref="System.Net.Http.HttpClient"/>. 
        /// This instance is used to manage HttpRequests for the <see cref="IRequest"/> that utilize it.
        /// </summary>
        public static HttpClient HttpClient
        {
            get
            {
                if (_httpClient == null)
                    lock (_lockObject) _httpClient ??= CreateHttpClient();
                return _httpClient;
            }
            set => _httpClient = value;
        }

        private static HttpClient CreateHttpClient()
        {
            SocketsHttpHandler handler = new()
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 10,
                SslOptions = new SslClientAuthenticationOptions { EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }
            };

            HttpClient client = new(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(100) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentBuilder.Generate());
            return client;
        }
    }
}