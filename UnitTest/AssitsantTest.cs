using DownloadAssistant.Requests;
using Requests;
using Requests.Options;

namespace UnitTest
{
    [TestClass]
    public class AssistantTest
    {
        [TestClass]
        public class RequestTests
        {
            private const string TestDirectory = "C:\\Bibliothek\\Downloads\\Test";

            [TestInitialize]
            public void TestInitialize()
            {
                Directory.CreateDirectory(TestDirectory);
            }


            [TestMethod]
            public async Task GetRequest_ShouldCompleteSuccessfully()
            {
                // Arrange
                GetRequest getRequest = new("https://www.learningcontainer.com/download/sample-large-zip-file/?wpdmdl=1639&refresh=6634d2642fcb31714737764", new()
                {
                    DirectoryPath = TestDirectory,
                    WriteMode = DownloadAssistant.Options.WriteMode.Append,
                    Filename = "GetRequest.zip",
                    MinReloadSize = 0,
                    MaxBytesPerSecond = 1000000,
                });

                bool isCompleted = false;
                getRequest.StateChanged += (sender, e) =>
                {
                    Console.WriteLine(e);
                    if (e == RequestState.Compleated)
                    {
                        isCompleted = true;
                    }
                };

                // Act
                await getRequest.Task;

                // Assert
                // Assert.IsTrue(isCompleted, "The request should complete successfully.");
                Assert.IsTrue(File.Exists(Path.Combine(TestDirectory, "GetRequest.zip")), "The file should be downloaded.");
            }

            [TestMethod]
            public async Task LoadRequest_ShouldCompleteSuccessfully()
            {
                // File.Copy(@"C:\Bibliothek\Downloads\Test\LoadRequest.zipK.part", @"C:\Bibliothek\Downloads\Test\LoadRequest.zip.part", true);
                // Arrange
                LoadRequest loadRequest = new("https://www.learningcontainer.com/download/sample-large-zip-file/?wpdmdl=1639&refresh=6634d2642fcb31714737764", new()
                {
                    DestinationPath = TestDirectory,
                    Filename = "LoadRequest.zip",
                    MinReloadSize = 1000,
                    WriteMode = DownloadAssistant.Options.WriteMode.Append,
                });

                bool isCompleted = false;
                loadRequest.StateChanged += (sender, e) =>
                {
                    if (e == RequestState.Compleated)
                    {
                        isCompleted = true;
                    }
                };

                // Act
                await loadRequest.Task;
                // Assert
                //Assert.IsTrue(isCompleted, "The request should complete successfully.");
                Assert.IsTrue(File.Exists(Path.Combine(TestDirectory, "LoadRequest.zip")), "The file should be downloaded.");
            }

            [TestMethod]
            public async Task PartialRequest_ShouldCompleteSuccessfully()
            {
                // Arrange
                RequestHandler.MainRequestHandlers[0].StaticDegreeOfParallelism = 3;
                LoadRequest loadRequest = new("https://www.learningcontainer.com/download/sample-large-zip-file/?wpdmdl=1639&refresh=6634d2642fcb31714737764", new()
                {
                    DestinationPath = TestDirectory,
                    Chunks = 7,
                    MergeWhileProgress = true,
                    WriteMode = DownloadAssistant.Options.WriteMode.AppendOrTruncate,
                    RequestCompleated = (req, url) => Console.WriteLine($"Finished successful: {url}"),
                });

                bool isCompleted = false;
                loadRequest.StateChanged += (sender, e) =>
                {
                    if (e == RequestState.Compleated)
                    {
                        isCompleted = true;
                    }
                };

                // Act
                await loadRequest.Task;
                //await Task.Delay(10000);

                // Assert
                // Assert.IsTrue(isCompleted, "The request should complete successfully.");
                Assert.IsTrue(File.Exists(Path.Combine(TestDirectory, "sample-large-zip-file.zip")), "The file should be downloaded.");
            }

            [TestMethod]
            public async Task PartialRequest_WithSubsequentRequest_ShouldCompleteSuccessfully()
            {
                // Arrange
                RequestHandler.MainRequestHandlers[0].StaticDegreeOfParallelism = 3;
                OwnRequest subRequest = new(async (token) =>
                {
                    Console.WriteLine("Subrequest started");
                    await Task.Delay(4000);
                    return true;
                }, new() { AutoStart = false });

                LoadRequest loadRequest = new("https://www.learningcontainer.com/download/sample-large-zip-file/?wpdmdl=1639&refresh=6634d2642fcb31714737764", new()
                {
                    DestinationPath = TestDirectory,
                    Chunks = 7,
                    SubsequentRequest = subRequest,
                    MergeWhileProgress = true,
                    WriteMode = DownloadAssistant.Options.WriteMode.AppendOrTruncate,
                    RequestCompleated = (req, url) => Console.WriteLine($"Finished successful: {url}"),
                });

                bool isCompleted = false;
                loadRequest.StateChanged += (sender, e) =>
                {
                    if (e == RequestState.Compleated)
                    {
                        isCompleted = true;
                    }
                };

                // Act
                await loadRequest.Task;
                Console.WriteLine("loadRequest finished");
                await subRequest.Task;

                // Assert
                //Assert.IsTrue(isCompleted, "The request should complete successfully.");
                Assert.IsTrue(File.Exists(Path.Combine(TestDirectory, "sample-large-zip-file.zip")), "The file should be downloaded.");
            }
        }
    }
}