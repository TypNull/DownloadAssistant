[![NuGet](https://img.shields.io/nuget/vpre/Shard.DownloadAssistant)](https://www.nuget.org/packages/Shard.DownloadAssistant) [![Downloads](https://img.shields.io/nuget/dt/Shard.DownloadAssistant)](https://www.nuget.org/packages/Shard.DownloadAssistant) [![License](https://img.shields.io/github/license/typnull/DownloadAssistant.svg)](https://github.com/typnull/downloadassistant/blob/master/LICENSE) ![Maintainability](https://img.shields.io/badge/Maintainability%20Index-85%25-brightgreen)
# DownloadAssistant
## _Shard Library to handle download Requests_

The download assistant library is based on .Net 6.0 and manages your download requests. This dowloader uses the [Shard.Requests](https://github.com/TypNull/Requests) library to handle the downloads.
It contains classes that help you to download your desired files.

_Tested with more than 5000 simultaneously HTTP Requests and file sized over 80Gb_

- Easy to use! 🔓
- Efficient ♾️ 
- ✨ Resume able file downloader! ✨


## Table of Contents
* [Features](#features)
* [Information](#tech)
* [Setup](#how-to-use)
* [ToDo](#TODO)

## Features
At the moment:
- **StatusRequest:** Calls a HEAD request and returns a response message with the header information.🔎
- **SiteRequest:** Is parsing a the HTML body of a website and list all references and files. 🔖
- **LoadRequest:** To download the response content into a file.
  - This is an HTTP file downloader with these functions:
  - *Pause* and *Start* a download ▶
  - Get the *filename* and *extension* from the server 📥
  - Timeout function ⌛
  - Monitor the progress of the download with `IProgress<float>`
  - Can set path and filename
  - Set download speed limit
  - Download a specified range of a file 🔛
  - Download a file into chunks ⛓️
  - Exclude extensions for safety _(.exe; .bat; etc...)_ 🛡️

## Tech
It is available on [Github](https://github.com/TypNull/DownloadAssistant):
- Repository: https://github.com/TypNull/DownloadAssistant
- Wiki: https://github.com/TypNull/DownloadAssistant/wiki
- Issues: https://github.com/TypNull/DownloadAssistant/issues


## Installation

Use [NuGet](https://img.shields.io/nuget/dt/Shard.DownloadAssistant) to install the actual version.

## How to use

Import the Library.
```cs
using DownloadAssistant.Request;
```
Then create a new `LoadRequest`.
This downloads a file into the download's folder of the PC with a ".part" file and uses the name that the server provides.
```cs
//To download a file and store it in "Downloads" folder
new LoadRequest("[Your URL]"); // e.g. https://www.sample-videos.com/video123/mkv/240/big_buck_bunny_240p_30mb.mkv
```
To set options on the `Request` create a `RequestOption` or for a `LoadRequest` a `LoadRequestOption`.
```cs
// Create an option for a LoadRequest
  LoadRequestOptions requestOptions = new()
        {
            // Sets the filename of the download without the extension
            // The extension will be added automatically!
            FileName = "downloadfile", 
            // If this download has priority (default is false)
            Priority = RequestPriority.High, 
            //(default is download folder)
            DestinationPath = "C:\\Users\\[Your Username]\\Desktop", 
            // If this Request contains a heavy request put it in second thread (default is false)
            IsDownload = true,
            //If the downloader should Override, Create a new file or Append (default is Append)
            //Resume function only available with append!
            Mode = LoadMode.Create,
            //Chunk a file to download faster
            Chunks = 3
        };
```
And use it in the request.
```cs
//To download a file and store it on the Desktop with a different name
new LoadRequest("https://speed.hetzner.de/100MB.bin",requestOptions);
```
To wait on the request, use *await* or *Wait();*.
```cs
await new LoadRequest("https://speed.hetzner.de/100MB.bin",requestOptions).Task;
//new LoadRequest("https://speed.hetzner.de/100MB.bin",requestOptions).Wait();
```
## TODO:
- M3U downloads
- Restart a download with previously downloaded length

## License

MIT

## **Free Code** and **Free to Use**
#### Have fun!
