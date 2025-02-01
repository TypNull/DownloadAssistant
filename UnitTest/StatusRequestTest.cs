using global::DownloadAssistant.Base;
using global::DownloadAssistant.Requests;
using RichardSzalay.MockHttp;
using System.Net;
using System.Net.Http.Headers;
using Xunit;
using Xunit.Abstractions;

namespace UnitTest.DownloadAssistant.Tests
{
    public class StatusRequestTests : IAsyncLifetime
    {
        private const string TestEndpoint = "http://example.com/resource";
        private const string TestContent = "This is a test content.";
        private static readonly MockHttpMessageHandler _mockHttpHandler = new();
        private readonly HttpClient _httpClient;
        private readonly ITestOutputHelper _output; // Add ITestOutputHelper

        // Inject ITestOutputHelper into the constructor
        public StatusRequestTests(ITestOutputHelper output)
        {
            _output = output;
            _httpClient = new HttpClient(_mockHttpHandler);
            HttpGet.HttpClient = _httpClient; // Assign the mocked HttpClient
        }

        public async Task InitializeAsync()
        {
            // Reset the handler to avoid contamination
            _mockHttpHandler.Clear();

            // Configure default handler for HEAD requests
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


            await Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _mockHttpHandler.Clear();
            _httpClient.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public void RunRequestAsync_Should_Fallback_To_Get_When_Head_NotAllowed()
        {
            _mockHttpHandler.Clear(); // Clear previous setups

            // Arrange
            _mockHttpHandler.When(HttpMethod.Head, TestEndpoint)
                .Respond(HttpStatusCode.MethodNotAllowed);

            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
                .Respond(async () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Test content")
                    {
                        Headers =
                        {
                    ContentType = new MediaTypeHeaderValue("text/plain"),
                    ContentLength = 100
                        }
                    }
                });

            HttpResponseMessage result = null;
            StatusRequest statusRequest = new(TestEndpoint, new()
            {
                RequestCompleated = (request, response) => result = response
            });

            // Act
            statusRequest.Wait();
            Task.Delay(2000).Wait();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("text/plain", statusRequest.FileType.Raw);
            Assert.Equal(100, statusRequest.ContentLength);
        }

        [Fact]
        public void RunRequestAsync_Should_ParseMetadata_From_HeadResponse()
        {
            _mockHttpHandler.Clear();
            // Arrange
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                // HEAD responses have no content
                Content = new ByteArrayContent(Array.Empty<byte>())
            };

            // Content headers must be set on Content.Headers
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            response.Content.Headers.ContentLength = 100;
            response.Content.Headers.ContentEncoding.Add("gzip");
            response.Content.Headers.ContentLanguage.Add("en-US");
            response.Content.Headers.LastModified = new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero);

            // General headers are set directly on response.Headers
            response.Headers.Server.Add(new ProductInfoHeaderValue("TestServer", "1.0"));
            response.Headers.ETag = new EntityTagHeaderValue("\"12345\"");
            response.Headers.AcceptRanges.Add("bytes");

            _mockHttpHandler.When(HttpMethod.Head, TestEndpoint).Respond(_ => response);

            HttpResponseMessage result = null;
            StatusRequest statusRequest = new(TestEndpoint, new()
            {
                RequestCompleated = (request, response) => result = response
            });

            // Act
            statusRequest.Wait();
            Task.Delay(1000).Wait();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("text/plain", statusRequest.FileType.Raw);
            Assert.Equal(100, statusRequest.ContentLength);
            Assert.Equal("TestServer/1.0", statusRequest.Server);
            Assert.Equal("\"12345\"", statusRequest.ETag);
            Assert.True(statusRequest.SupportsRangeRequests);
            Assert.Equal("gzip", statusRequest.ContentEncoding);
            Assert.Equal("en-US", statusRequest.ContentLanguage);
            Assert.Equal(new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero), statusRequest.LastModified);
        }


        [Fact]
        public void RunRequestAsync_Should_Handle_Exceptions()
        {
            _mockHttpHandler.Clear();
            // Arrange
            _mockHttpHandler.When(HttpMethod.Head, TestEndpoint)
                .Throw(new HttpRequestException("Test exception"));

            HttpResponseMessage result = null;
            StatusRequest statusRequest = new(TestEndpoint, new()
            {
                RequestCompleated = (request, response) => result = response
            });

            // Act
            statusRequest.Wait();
            Task.Delay(1000).Wait();

            // Assert
            Assert.Null(result);
            Assert.NotNull(statusRequest.Exception);
        }

        [Fact]
        public void IsLikelyDownloadable_Should_Return_True_For_Media_Content()
        {
            _mockHttpHandler.Clear();
            _mockHttpHandler.When(HttpMethod.Head, TestEndpoint)
             .Respond(async () => new HttpResponseMessage(HttpStatusCode.OK)
             {
                 Content = new ByteArrayContent(new byte[] { 7, 8, 9, 6, 8, 9 })
                 {
                     Headers =
                     {
                    ContentType = new MediaTypeHeaderValue("video/mp4")
                     }
                 }
             });

            HttpResponseMessage result = null;
            StatusRequest statusRequest = new(TestEndpoint, new()
            {
                RequestCompleated = (request, response) =>
                {
                    result = response;
                }
            });


            // Act
            statusRequest.Wait();
            Task.Delay(1000).Wait();

            // Assert
            Assert.True(statusRequest.IsLikelyDownloadable());
        }

        [Fact]
        public void SupportsResume_Should_Return_True_For_Resumable_Content()
        {
            _mockHttpHandler.Clear();
            // Arrange
            _mockHttpHandler.When(HttpMethod.Head, TestEndpoint)
                .Respond(async () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                    {
                        Headers =
                        {
                            ContentLength = 100
                        }
                    },
                    Headers =
                    {
                        AcceptRanges = { "bytes" }
                    }
                });

            HttpResponseMessage result = null;
            StatusRequest statusRequest = new(TestEndpoint, new()
            {
                RequestCompleated = (request, response) => result = response
            });

            // Act
            statusRequest.Wait();
            Task.Delay(1000).Wait();

            // Assert
            Assert.True(statusRequest.SupportsResume());
        }
    }
}