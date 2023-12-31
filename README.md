﻿[![NuGet](https://img.shields.io/nuget/vpre/Shard.DownloadAssistant)](https://www.nuget.org/packages/Shard.DownloadAssistant) [![Downloads](https://img.shields.io/nuget/dt/Shard.DownloadAssistant)](https://www.nuget.org/packages/Shard.DownloadAssistant) [![License](https://img.shields.io/github/license/typnull/DownloadAssistant.svg)](https://github.com/typnull/downloadassistant/blob/master/LICENSE.txt) ![Maintainability](https://img.shields.io/badge/Maintainability%20Index-85%25-brightgreen)
# DownloadAssistant

Introducing the Download Assistant Library for .Net 6.0! 🚀

Looking for a powerful tool to effortlessly manage your download requests? Look no further! 🙌

🔗 Built on top of the [Shard.Requests](https://github.com/TypNull/Requests) library, our Download Assistant Library is here to handle all your downloads with ease. 💪

With its intuitive classes, this library simplifies the process of downloading your desired files. Perfect for beginners, it offers a seamless user experience and great download capabilities. 💯

Simply create an object and let our assistant take care of the rest! ⚡️

⏳ Worried about failed downloads? Our assistant automatically retries downloads and tries to ensure a successful completion.

✨ Additionally, if the server throttles the connection, our assistant has the capability to download a file in parts, significantly speeding up the download time.

Get started with the Download Assistant Library today and experience the convenience and efficiency it brings to your download workflow. 🌟

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
  - Note!  

## Tech
It is available on [Github](https://github.com/TypNull/DownloadAssistant):
- Repository: https://github.com/TypNull/DownloadAssistant
- Wiki: https://github.com/TypNull/DownloadAssistant/wiki
- Issues: https://github.com/TypNull/DownloadAssistant/issues


## Installation

Use [NuGet](https://img.shields.io/nuget/dt/Shard.DownloadAssistant) Packagemanager to install the actual version for your project.

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
The function to restart a download with previously downloaded length for LoadRequest, when WriteMode is set to Append, has not been implemented yet.

## License

MIT

## **Free Code** and **Free to Use**
#### Have fun!
