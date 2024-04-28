using DownloadAssistant.Request;
using Requests;
using Requests.Options;

namespace UnitTest
{
    [TestClass]
    public class AssistantTest
    {
        [TestMethod]
        public async Task GetRequestTest()
        {
            GetRequest getRequest = new("https://www.openprinting.org/download/testfiles/pclm-test20210804.tar.xz", new()
            {
                DirectoryPath = "D:\\Bibliothek\\Downloads\\Test",
                Filename = "GetRequest.test",
                MaxBytesPerSecond = 100000,

            });


            getRequest.StateChanged += (object? sender, RequestState e) => Console.WriteLine($"State Changed: {e} | {(sender as GetRequest)?.Url}");
            await getRequest.Task;
            Console.WriteLine("Task finished");
        }



        [TestMethod]
        public async Task LoadRequestTest()
        {
            LoadRequest loadRequest = new("https://www.openprinting.org/download/testfiles/pclm-test20210804.tar.xz", new()
            {
                DestinationPath = "D:\\Bibliothek\\Downloads\\Test",
                Filename = "LoadRequest.test",
                WriteMode = DownloadAssistant.Options.WriteMode.Append
            });
            loadRequest.StateChanged += (object? sender, RequestState e) => Console.WriteLine($"State Changed: {e} | {(sender as GetRequest)?.Url}");
            await loadRequest.Task;
            Console.WriteLine("Task finished");
        }

        [TestMethod]
        public async Task PartialRequestTest()
        {
            RequestHandler.MainRequestHandlers[0].StaticDegreeOfParallelism = 10;
            LoadRequest loadRequest = new("https://www.openprinting.org/download/testfiles/pclm-test20210804.tar.xz", new()
            {
                DestinationPath = "D:\\Bibliothek\\Downloads\\Test",
                Chunks=10,
                MergeWhileProgress= true,
                Filename = "LoadRequest.test",
                WriteMode = DownloadAssistant.Options.WriteMode.Append
            });
            loadRequest.StateChanged += (object? sender, RequestState e) => Console.WriteLine($"State Changed: {e} | {(sender as GetRequest)?.Url}");
            await Task.Delay(3000);
            Console.WriteLine("Task finished");
        }


    }
}