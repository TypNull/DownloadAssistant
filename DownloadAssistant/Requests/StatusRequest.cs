using DownloadAssistant.Base;
using DownloadAssistant.Options;
using Requests;
using System.Diagnostics;

namespace DownloadAssistant.Requests
{
    /// <summary>
    /// Represents a web request that only retrieves the headers of a URL.
    /// This is achieved by sending a <see cref="HttpMethod.Head"/> request.
    /// </summary>
    public class StatusRequest : WebRequest<WebRequestOptions<HttpResponseMessage>, HttpResponseMessage>
    {
        /// <summary>
        /// A <see cref="CancellationTokenSource"/> used to cancel the request after a specified <see cref="WebRequestOptions{TCompleated}.Timeout"/>.
        /// </summary>
        private CancellationTokenSource? _timeoutCTS;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusRequest"/> class.
        /// </summary>
        /// <param name="url">The URL from which to retrieve the head response.</param>
        /// <param name="options">The options used to modify the <see cref="Request{TOptions, TCompleated, TFailed}"/>.</param>
        public StatusRequest(string url, WebRequestOptions<HttpResponseMessage>? options = null) : base(url, options) => AutoStart();

        /// <summary>
        /// Executes the <see cref="StatusRequest"/> that the <see cref="HttpClient"/> should start.
        /// </summary>
        /// <returns>A <see cref="Request{TOptions, TCompleated, TFailed}.RequestReturn"/> object indicating whether the <see cref="StatusRequest"/> was successful and containing the return objects.</returns>
        protected override async Task<RequestReturn> RunRequestAsync()
        {
            RequestReturn returnObject = new();
            try
            {
                using HttpResponseMessage res = await SendHttpMenssage();
                SetRequestReturn(returnObject, res);
                _timeoutCTS?.Dispose();
            }
            catch (Exception ex)
            {
                AddException(ex);
                Debug.Assert(false, ex.Message);
                returnObject.Successful = false;
            }
            return returnObject;
        }

        /// <summary>
        /// Populates the <see cref="Request{TOptions, TCompleated, TFailed}.RequestReturn"/> object with data from the <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <param name="returnObject">The <see cref="Request{TOptions, TCompleated, TFailed}.RequestReturn"/> object to populate.</param>
        /// <param name="res">The <see cref="HttpResponseMessage"/> from which to extract data.</param>
        private static void SetRequestReturn(RequestReturn returnObject, HttpResponseMessage res)
        {
            returnObject.CompleatedReturn = res;
            returnObject.FailedReturn = res;
            if (res.IsSuccessStatusCode)
                returnObject.Successful = true;
        }

        /// <summary>
        /// Creates an <see cref="HttpRequestMessage"/> and sends it.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessage"/> resulting from the request.</returns>
        private async Task<HttpResponseMessage> SendHttpMenssage()
        {
            SetTimedToken();
            HttpRequestMessage request = GetPresetRequestMessage(new(HttpMethod.Head, _uri.AbsoluteUri));
            return await HttpGet.HttpClient.SendAsync(request, _timeoutCTS!.Token);
        }

        /// <summary>
        /// Sets up a cancellation token that will cancel the request after a specified timeout.
        /// </summary>
        private void SetTimedToken()
        {
            _timeoutCTS?.Dispose();
            _timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(Token);
            _timeoutCTS.CancelAfter(Options.Timeout ?? TimeSpan.FromSeconds(10));
        }
    }
}