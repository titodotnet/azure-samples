
# azure-samples
## Introduction
This repository is to hold the small azure samples.

## AzureSampleConsoleApp1
### Sample for Chunk download from Web and upload into Blobs as chunks
 - `ChunkDownloadLargeFileAndUploadToBlob.cs` holds the sample describing how to download a resource as chunk and then upload them in Azure BLOB as chunks.
 
#### Intro
In real case scenarios there are time where we need to download large files from from a Web resource and save it to Azure BLOB storage. Even though there are few articles over Web which helped, I'm not able to get a end to end working solution for files with size ranging from 20GB to 30GB. Have detailed below challenges and related solutions for your convenience.

#### Problem statement
Download large files from the Web resource and upload them into Azure BLOB storage.

#### Challenges and Solutions
 1. First we'll hit memory issue when we try to read the full stream and load all bytes into memory. An article [here](http://www.tugberkugurlu.com/archive/efficiently-streaming-large-http-responses-with-httpclient) explains well how to avoid this Memory issue.
 1. Another article [here](https://www.red-gate.com/simple-talk/cloud/platform-as-a-service/azure-blob-storage-part-4-uploading-large-blobs/) detailed how to read FileStream  as chunks and upload them into BLOB. Using the details from this and #1 we can try to achieve he solution. 
    1. Use Stream.Read by passing respective parameters. Points to note here are, In that one of the parameter is maximum number of bytes to read from the current stream. And the Stream.Read method returns the number of bytes read. But that also got into issue as the Stream.Read method returns total number of bytes read which can be less than the maximum count parameter. Detailed documentation of Stream.Read can be found [here](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.read?view=netframework-4.7.1). 
    1. Along with that, we can use [`CloudBlockBlob.PutBlock`](https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.storage.blob.cloudblockblob.putblockasync?view=azure-dotnet) method to upload read chunks into BLOB. Points to note here are, each block (chunk) in BLOB can be maximum of 100MB in size and at the max you can have 50000 blocks (100MB x 50000 blocks = 4.75TB). Detailed documentation can be found [here](https://docs.microsoft.com/en-us/rest/api/storageservices/put-block-list#remarks). 
    1. Even though #2.1 and #2.2 looks straight forward, there might be issues because Read can read less number of bytes which in turn increases the number of blocks (more than 50000 limit) in the Azure BLOB storage.
    1. In order to avoid issue mentioned above in #2.3, we need to accumulate the resultant of  Stream.Read till it reaches the expected size (100MB in our case).
 1. Even though you fix the issues specified in #1 and #2 above, you might face exception stating An existing connection was forcibly closed by the remote host. . Though I'm not able to identify the solution for this, have found workaround using the articles [here](https://tutel.me/c/programming/questions/33233780/systemnethttphttprequestexception+error+while+copying+content+to+a+stream) and [here](https://social.msdn.microsoft.com/Forums/en-US/c620ce2c-c512-4c9f-a481-521ecd260039/systemioioexception-unable-to-read-data-from-the-transport-connection-the-connection-was-closed?forum=vstswebtest). Use Http1.0 instead of Http1.1.

Code sample can be found [here](/AzureSamples/AzureSampleConsoleApp1/ChunkDownloadLargeFileAndUploadToBlob.cs).
