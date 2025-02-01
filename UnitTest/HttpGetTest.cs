using global::DownloadAssistant.Base;
using RichardSzalay.MockHttp;
using System.Net;
using Xunit;

namespace UnitTest.DownloadAssistant.Tests
{
    public class HttpGetTests : IAsyncLifetime
    {
        private static readonly MockHttpMessageHandler _mockHttpHandler = new();
        private const string TestContent = "Test content for HttpGet tests";
        private const string TestEndpoint = "http://mock/test-resource";


        public async Task InitializeAsync()
        {
            // Configure static handler once for all tests
            _mockHttpHandler.When(HttpMethod.Head, TestEndpoint)
                .Respond(_ => new HttpResponseMessage
                {
                    Content = new StringContent(TestContent) // Auto-sets Content-Length
                });

            // Handler for partial content requests (priority)
            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
     .With(req => req.Headers.Range != null) // Match any Range header
     .Respond(req =>
     {
         // Parse range from request (format: "bytes=0-10")
         string rangeHeader = req.Headers.Range.ToString();
         string[] rangeValues = rangeHeader.Replace("bytes=", "").Split('-');
         long start = long.Parse(rangeValues[0]);
         long end = long.Parse(rangeValues[1]);

         // Ensure the range is valid
         if (start < 0 || end >= TestContent.Length)
         {
             return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
         }

         // Calculate the length of the content to return
         int length = (int)(end - start + 1);

         // Create partial content response
         StringContent content = new(TestContent.Substring((int)start, length));
         content.Headers.Add("Content-Range", $"bytes {start}-{end}/{TestContent.Length}");

         return new HttpResponseMessage(HttpStatusCode.PartialContent)
         {
             Content = content
         };
     });

            // Fallback handler for full content requests (no Range header)
            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
                .Respond(_ => new HttpResponseMessage
                {
                    Content = new StringContent(TestContent)
                });

            // Handler for slow requests
            _mockHttpHandler.When(HttpMethod.Get, "http://mock/slow")
                .Respond(async () =>
                {
                    await Task.Delay(2000);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            // Assign static HttpClient
            HttpGet.HttpClient = new HttpClient(_mockHttpHandler);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void Should_Initialize_With_Valid_Request()
        {
            using HttpRequestMessage request = new(HttpMethod.Get, TestEndpoint);
            using HttpGet httpGet = new(request);

            Assert.NotNull(httpGet);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.False(httpGet.HasToBePartial());
        }

        [Fact]
        public async Task Should_Detect_Full_Content_Length()
        {
            using HttpRequestMessage request = new(HttpMethod.Get, TestEndpoint);
            using HttpGet httpGet = new(request);

            HttpResponseMessage response = await httpGet.LoadResponseAsync();

            Assert.Equal(TestContent.Length, httpGet.FullContentLength);
            Assert.Equal(TestContent.Length, response.Content.Headers.ContentLength);
        }

        [Fact]
        public async Task Should_Handle_Partial_Content_Range()
        {
            using HttpRequestMessage request = new(HttpMethod.Get, TestEndpoint);
            using HttpGet httpGet = new(request)
            {
                Range = new LoadRange(0L, 10L)
            };

            HttpResponseMessage response = await httpGet.LoadResponseAsync();

            Assert.True(httpGet.IsPartial());
            Assert.Equal(11, httpGet.PartialContentLength);
            Assert.Equal(206, (int)response.StatusCode);
        }

        [Fact]
        public async Task Should_Combine_Ranges_Correctly()
        {
            using HttpRequestMessage request = new(HttpMethod.Get, TestEndpoint);
            using HttpGet httpGet = new(request)
            {
                Range = new LoadRange(0L, 10L),
                SecondRange = new LoadRange(5L, 15L)
            };

            HttpResponseMessage response = await httpGet.LoadResponseAsync();

            // Assertions
            Assert.Equal(TestContent.Length, httpGet.FullContentLength);
            Assert.Equal(5, httpGet.Range.Start);
            Assert.Equal(10, httpGet.Range.End);
            Assert.Equal(6, httpGet.PartialContentLength);
        }

        [Fact]
        public async Task Should_Handle_Added_Bytes_To_Start()
        {
            using HttpRequestMessage request = new(HttpMethod.Get, TestEndpoint);
            using HttpGet httpGet = new(request)
            {
                Range = new LoadRange(10L, 20L)
            };

            httpGet.AddBytesToStart(5);
            HttpResponseMessage response = await httpGet.LoadResponseAsync();

            Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);

            // Assert that the response has the correct Content-Range header
            Assert.NotNull(response.Content.Headers.ContentRange);
            Assert.Equal(15, response.Content.Headers.ContentRange.From);
            Assert.Equal(20, response.Content.Headers.ContentRange.To);
            Assert.True(response.Content.Headers.ContentRange.Length.HasValue);
        }

        [Fact]
        public async Task Should_Dispose_Resources_Correctly()
        {
            HttpGet httpGet = new(new HttpRequestMessage(HttpMethod.Get, TestEndpoint));
            httpGet.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => httpGet.LoadResponseAsync());
        }

        [Fact]
        public async Task Should_Handle_Timeout_Correctly()
        {
            using HttpRequestMessage request = new(HttpMethod.Get, "http://mock/slow");
            using HttpGet httpGet = new(request)
            {
                Timeout = TimeSpan.FromMilliseconds(100)
            };

            await Assert.ThrowsAsync<TaskCanceledException>(() => httpGet.LoadResponseAsync());
        }
    }
}