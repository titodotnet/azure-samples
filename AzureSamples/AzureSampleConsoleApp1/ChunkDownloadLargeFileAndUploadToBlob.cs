using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AzureSampleConsoleApp1
{
    public class ChunkDownloadLargeFileAndUploadToBlob
    {        
        /// <summary>
        /// Entry point.
        /// </summary>
        public static void Main()
        {
            var largeFileProcessor = new LargeFileProcessor();
            largeFileProcessor.ProcessLargeFile().Wait();

            Console.ReadKey();
        }

    }

    public class LargeFileProcessor
    {
        /// <summary>
        /// Logger instance.
        /// </summary>
        private ILogger logger = new Logger();

        /// <summary>
        /// Retry count.
        /// </summary>
        private int retryCount = 5;

        /// <summary>
        /// Time delay for retry.
        /// </summary>
        private TimeSpan delay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Download Large File as chunk and upload as chunk into BLOB.
        /// </summary>
        public async Task ProcessLargeFile()
        {
            // Create Storage account reference.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageAccount"));

            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a container.
            CloudBlobContainer container = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("ContainerName"));
            container.CreateIfNotExists();

            // Create Blob reference.
            CloudBlockBlob blob = container.GetBlockBlobReference(CloudConfigurationManager.GetSetting("BlobFileName"));

            string urlToDownload = CloudConfigurationManager.GetSetting("DownloadURL"); // Provide valid URL from where the large file can be downloaded.

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri(urlToDownload))
                    {
                        // To avoid error related to 'An existing connection was forcibly closed by the remote host'. Use Http1.0 instead of Http1.1.
                        Version = HttpVersion.Version10
                    };

                    using (HttpResponseMessage response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                        using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            const int pageSizeInBytes = 104857600; // 100MB. As Blob chunk max size is 100MB as of now.

                            var blockIds = new List<string>();
                            var sha256 = new SHA256Managed();

                            var bytesRemaing = response.Content.Headers.ContentLength.Value; // Read Total file size from the header.
                            int blockIdentifier = 0;

                            while (bytesRemaing > 0)
                            {
                                blockIdentifier++;
                                var bytesToCopy = (int)Math.Min(bytesRemaing, pageSizeInBytes);
                                var bytesToSend = new byte[bytesToCopy];

                                var bytesCountRead = await ReadStreamAndAccumulate(stream, bytesToSend, bytesToCopy);

                                // Instead of calculating bytes remaining to exit the While loop,  we can use bytesCountRead as bytesCountRead will be 0 when there are no more bytes to read form the stream.   
                                bytesRemaing -= bytesCountRead;

                                this.logger.WriteLine($"bytes read: {bytesCountRead}");
                                this.logger.WriteLine($"bytes remaining: {bytesRemaing}");

                                string base64BlockId = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(string.Format("largefile1BlockId{0}", blockIdentifier.ToString("0000000"))));
                                blockIds.Add(base64BlockId);

                                // Calculate the checksum value.
                                if (bytesRemaing <= 0)
                                {
                                    sha256.TransformFinalBlock(bytesToSend, 0, bytesCountRead);
                                }
                                else
                                {
                                    sha256.TransformBlock(bytesToSend, 0, bytesCountRead, bytesToSend, 0);
                                }

                                await blob.PutBlockAsync(base64BlockId, new MemoryStream(bytesToSend), null);
                            }

                            var checksum = BitConverter.ToString(sha256.Hash).Replace("-", string.Empty);
                            this.logger.WriteLine($"Hash value is : {checksum}");
                            await blob.PutBlockListAsync(blockIds);

                            await Task.FromResult(0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.WriteLine(ex.Message);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                this.logger.WriteLine($"Execution time in mins: {stopwatch.Elapsed.TotalMinutes}");
            }
        }

        /// <summary>
        /// Read the stream and accumulate till it reaches the number of bytes specified to copy.
        /// </summary>
        /// <param name="stream">Stream to be read from.</param>
        /// <param name="bytesToSend">Target byte array that holds the bytes read.</param>
        /// <param name="bytesCountToCopy">The number of bytes to be copied.</param>
        /// <returns>The number of bytes read.</returns>
        private async Task<int> ReadStreamAndAccumulate(Stream stream, byte[] bytesToSend, int bytesCountToCopy)
        {
            int bytesReadSoFar = 0;

            while (bytesReadSoFar < bytesCountToCopy)
            {
                var currentBytesCountRead = await ReadStreamWithRetry(stream, bytesToSend, bytesCountToCopy - bytesReadSoFar, bytesReadSoFar).ConfigureAwait(false);
                bytesReadSoFar += currentBytesCountRead;
            }

            return bytesReadSoFar;
        }

        /// <summary>
        /// Reads the stream with retry when failed. 
        /// </summary>
        /// <param name="stream">Stream to be read from.</param>
        /// <param name="bytesToSend">Target byte array that holds the bytes read.</param>
        /// <param name="bytesCountToCopy">The number of bytes to be copied.</param>
        /// <param name="offset">The byte offset in buffer at which to begin writing data from the stream.</param>
        /// <returns>The number of bytes read.</returns>
        private async Task<int> ReadStreamWithRetry(Stream stream, byte[] bytesToSend, int bytesCountToCopy, int offset)
        {
            int currentRetry = 0;
            for (; ; )
            {
                try
                {
                    var bytesRead = await stream.ReadAsync(bytesToSend, offset, bytesCountToCopy);
                    return bytesRead;
                }
                catch (Exception ex)
                {
                    this.logger.WriteLine($"Operation Exception : {ex.Message}");

                    currentRetry++;

                    // Check if it is within the retry count specified.
                    if (currentRetry > this.retryCount)
                    {
                        // Rethrow the exception if it more than the retry attempt.
                        throw;
                    }
                }

                // Wait to retry the operation.
                await Task.Delay(delay);
            }

        }
    }
}
