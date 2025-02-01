using DownloadAssistant.Base;
using global::DownloadAssistant.Requests;
using Requests.Options;
using RichardSzalay.MockHttp;
using Xunit;
using Xunit.Abstractions;

namespace UnitTest.DownloadAssistant.Tests
{

    public class SiteRequestTests
    {
        private readonly MockHttpMessageHandler _mockHttpHandler;
        private const string TestEndpoint = "https://example.com";
        private readonly ITestOutputHelper _output; // Add ITestOutputHelper


        public SiteRequestTests(ITestOutputHelper output)
        {
            _output = output;
            // Initialize the mock HTTP handler
            _mockHttpHandler = new MockHttpMessageHandler();
            HttpGet.HttpClient = new HttpClient(_mockHttpHandler);
        }

        [Fact]
        public void RunRequestAsync_Should_ReturnFailure_ForNonHtmlContent()
        {
            // Arrange
            _mockHttpHandler.Clear(); // Clear previous setups

            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint).Respond("application/json", "Not HTML");

            SiteRequest siteRequest = new(TestEndpoint);

            // Act
            siteRequest.Wait();

            // Assert
            Assert.True(siteRequest.State == RequestState.Failed);
        }

        [Fact]
        public void RunRequestAsync_Should_ReturnSuccess_ForHtmlContent()
        {
            // Arrange
            _mockHttpHandler.Clear(); // Clear previous setups

            string htmlContent = "<html><body><img src='image.png'></body></html>";
            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
                .Respond("text/html", htmlContent);

            SiteRequest siteRequest = new(TestEndpoint);

            // Act
            siteRequest.Wait();

            // Assert
            Assert.True(siteRequest.State == RequestState.Compleated);
            Assert.Equal(htmlContent, siteRequest.HTML);
        }

        [Fact]
        public void RunRequestAsync_Should_ExtractImageUrls_Correctly()
        {
            // Arrange
            _mockHttpHandler.Clear(); // Clear previous setups

            const string htmlContent = "<html><body><img src='https://example.com/image.png'></body></html>";
            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
                .Respond("text/html", htmlContent);

            SiteRequest siteRequest = new(TestEndpoint);

            // Act
            siteRequest.Wait();

            // Assert
            _output.WriteLine(siteRequest.State.ToString());
            Assert.True(siteRequest.State == RequestState.Compleated);
            Assert.Single(siteRequest.Images);
            Assert.Equal("https://example.com/image.png", siteRequest.Images[0].URL.ToString());
        }

        [Fact]
        public void RunRequestAsync_Should_NormalizeRelativeUrls_Correctly()
        {
            // Arrange
            _mockHttpHandler.Clear(); // Clear previous setups

            string htmlContent = "<html><body><img src='/image.png'></body></html>";
            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
                .Respond("text/html", htmlContent);

            SiteRequest siteRequest = new(TestEndpoint);

            // Act
            siteRequest.Wait();

            // Assert
            Assert.True(siteRequest.State == RequestState.Compleated);
            Assert.Equal(htmlContent, siteRequest.HTML);
            Assert.Single(siteRequest.Images);
            Assert.Equal("https://example.com/image.png", siteRequest.Images[0].URL.ToString());
        }

        [Fact]
        public void RunRequestAsync_Should_ExtractCssUrls_Correctly()
        {
            // Arrange
            _mockHttpHandler.Clear(); // Clear previous setups

            string htmlContent = "<html><head><link rel='stylesheet' href='styles.css'></head></html>";
            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
                .Respond("text/html", htmlContent);

            SiteRequest siteRequest = new(TestEndpoint);

            // Act
            siteRequest.Wait();

            // Assert
            Assert.True(siteRequest.State == RequestState.Compleated);
            Assert.Single(siteRequest.CSS);
            Assert.Equal("https://example.com/styles.css", siteRequest.CSS[0].URL.ToString());
        }

        [Fact]
        public void RunRequestAsync_Should_ExtractScriptUrls_Correctly()
        {
            // Arrange
            _mockHttpHandler.Clear(); // Clear previous setups

            string htmlContent = "<html><head><script src='script.js'></script></head></html>";
            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
                .Respond("text/html", htmlContent);

            SiteRequest siteRequest = new(TestEndpoint);

            // Act
            siteRequest.Wait();

            // Assert
            Assert.True(siteRequest.State == RequestState.Compleated);
            Assert.Single(siteRequest.Scripts);
            Assert.Equal("https://example.com/script.js", siteRequest.Scripts[0].URL.ToString());
        }

        [Fact]
        public void RunRequestAsync_Should_CategorizeUnknownTypes_Correctly()
        {
            // Arrange
            _mockHttpHandler.Clear(); // Clear previous setups

            string htmlContent = "<html><body><a href='unknown.xyz'>Link</a></body></html>";
            _mockHttpHandler.When(HttpMethod.Get, TestEndpoint)
                .Respond("text/html", htmlContent);

            SiteRequest siteRequest = new(TestEndpoint);

            // Act
            siteRequest.Wait();

            // Assert
            Assert.True(siteRequest.State == RequestState.Compleated);
            Assert.Single(siteRequest.UnknownType);
            Assert.Equal("https://example.com/unknown.xyz", siteRequest.UnknownType[0].URL.ToString());
        }
    }
}