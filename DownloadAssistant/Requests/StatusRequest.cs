using DownloadAssistant.Base;
using DownloadAssistant.Options;
using System.Diagnostics;

namespace DownloadAssistant.Request
{
    /// <summary>
    /// Request that only gets the Headers of an URL.
    /// Send a <see cref="HttpMethod.Head"/> request.
    /// </summary>
    public class StatusRequest : WebRequest<WebRequestOptions<HttpResponseMessage>, HttpResponseMessage>
    {
        /// <summary>
        /// A <see cref="CancellationTokenSource"/> that will be used to let <see cref="WebRequestOptions{TCompleated}.Timeout"/> run.
        /// </summary>
        private CancellationTokenSource? _timeoutCTS;

        /// <summary>
        /// Constructor for the <see cref="StatusRequest"/>
        /// CompleatedAction returns a <see cref="HttpResponseMessage"/>
        /// </summary>
        /// <param name="url">URL to get the Head response from</param>
        /// <param name="options">Options to modify the <see cref="Request"/></param>
        public StatusRequest(string url, WebRequestOptions<HttpResponseMessage>? options = null) : base(url, options) => AutoStart();

        /// <summary>
        /// Handles the <see cref="StatusRequest"/> that the <see cref="HttpClient"/> should start.
        /// </summary>
        /// <returns>A RequestReturn object that indicates if <see cref="StatusRequest"/> was succesful and returns the return objects.</returns>
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
        /// Sets elements to the ReturnObject
        /// </summary>
        /// <param name="returnObject">Object to set</param>
        /// <param name="res">Response to get Elements</param>
        private static void SetRequestReturn(RequestReturn returnObject, HttpResponseMessage res)
        {
            returnObject.CompleatedReturn = res;
            returnObject.FailedReturn = res;
            if (res.IsSuccessStatusCode)
                returnObject.Successful = true;
        }

        /// <summary>
        /// Creates a HttpRequestMessage and send it.
        /// </summary>
        /// <returns>A response as <see cref="HttpResponseMessage"/></returns>
        private async Task<HttpResponseMessage> SendHttpMenssage()
        {
            SetTimedToken();
            HttpRequestMessage request = GetPresetRequestMessage(new(HttpMethod.Head, _uri.AbsoluteUri));
            return await HttpGet.HttpClient.SendAsync(request, _timeoutCTS!.Token);
        }

        private void SetTimedToken()
        {
            _timeoutCTS?.Dispose();
            _timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(Token);
            _timeoutCTS.CancelAfter(Options.Timeout ?? TimeSpan.FromSeconds(10));
        }
    }
}