// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Microsoft.Build.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Build.CloudTestTasks
{


    public class UploadClient
    {
        private TaskLoggingHelper log;

        public UploadClient(TaskLoggingHelper loggingHelper)
        {
            log = loggingHelper;
        }

        public string EncodeBlockIds(int numberOfBlocks, int lengthOfId)
        {
            string numberOfBlocksString = numberOfBlocks.ToString("D" + lengthOfId);

            byte[] bytes = Encoding.UTF8.GetBytes(numberOfBlocksString);
            return Convert.ToBase64String(bytes);
        }

        public async Task UploadBlockBlobAsync(
            CancellationToken ct,
            string AccountName,
            string AccountKey,
            string ContainerName,
            string filePath,
            string destinationBlob)
        {
           
            string resourceUrl = string.Format("https://{0}.blob.core.windows.net/{1}", AccountName, ContainerName);

            string fileName = destinationBlob;
            fileName = fileName.Replace("\\", "/");
            string blobUploadUrl = resourceUrl + "/" + fileName;
            int size = (int)new FileInfo(filePath).Length;
            int blockSize = 4 * 1024 * 1024; //4MB max size of a block blob
            int bytesLeft = size;
            List<string> blockIds = new List<string>();
            int numberOfBlocks = (size / blockSize) + 1;
            int countForId = 0;
            using (FileStream fileStreamTofilePath = new FileStream(filePath, FileMode.Open))
            {
                int offset = 0;

                while (bytesLeft > 0)
                {
                    int nextBytesToRead = (bytesLeft < blockSize) ? bytesLeft : blockSize;
                    byte[] fileBytes = new byte[blockSize];
                    int read = fileStreamTofilePath.Read(fileBytes, 0, nextBytesToRead);

                    if (nextBytesToRead != read)
                    {
                        log.LogError(
                            "Number of bytes read ({0}) from file {1} isn't equal to the number of bytes expected ({1}) .",
                            read,
                            fileName,
                            nextBytesToRead);
                    }

                    string blockId = EncodeBlockIds(countForId, numberOfBlocks.ToString().Length);

                    blockIds.Add(blockId);
                    string blockUploadUrl = blobUploadUrl + "?comp=block&blockid=" + blockId;

                    DateTime dt = DateTime.UtcNow;
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Clear();
                        using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Put, blockUploadUrl))
                        {
                            req.Headers.Add(
                                AzureHelper.DateHeaderString,
                                dt.ToString("R", CultureInfo.InvariantCulture));
                            req.Headers.Add(AzureHelper.VersionHeaderString, AzureHelper.StorageApiVersion);
                            req.Headers.Add(
                                AzureHelper.AuthorizationHeaderString,
                                AzureHelper.AuthorizationHeader(
                                    AccountName,
                                    AccountKey,
                                    "PUT",
                                    dt,
                                    req,
                                    string.Empty,
                                    string.Empty,
                                    nextBytesToRead.ToString(),
                                    string.Empty));

                            log.LogMessage("Sending request to upload part {0} of file {1}", countForId, fileName);

                            using (Stream postStream = new MemoryStream())
                            {
                                postStream.Write(fileBytes, 0, nextBytesToRead);
                                postStream.Seek(0, SeekOrigin.Begin);
                                StreamContent contentStream = new StreamContent(postStream);
                                req.Content = contentStream;
                                using (HttpResponseMessage response = await client.SendAsync(req, ct))
                                {
                                    this.log.LogMessage(
                                        "Received response to upload part {0} of file {1}: Status Code:{2} Status Desc: {3}",
                                        countForId,
                                        fileName,
                                        response.StatusCode,
                                        await response.Content.ReadAsStringAsync());
                                }
                            }
                        }
                    }
                    offset += read;
                    bytesLeft -= nextBytesToRead;
                    countForId += 1;
                }
            }

            string blockListUploadUrl = blobUploadUrl + "?comp=blocklist";
            DateTime dt1 = DateTime.UtcNow;
            using (HttpClient client = new HttpClient())
            {
                using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Put, blockListUploadUrl))
                {
                    req.Headers.Add(AzureHelper.DateHeaderString, dt1.ToString("R", CultureInfo.InvariantCulture));
                    req.Headers.Add(AzureHelper.VersionHeaderString, AzureHelper.StorageApiVersion);

                    string body = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><BlockList>";
                    foreach (object item in blockIds)
                    {
                        body += "<Latest>" + item + "</Latest>";
                    }

                    body += "</BlockList>";
                    byte[] bodyData = Encoding.UTF8.GetBytes(body);
                    req.Headers.Add(
                        AzureHelper.AuthorizationHeaderString,
                        AzureHelper.AuthorizationHeader(
                            AccountName,
                            AccountKey,
                            "PUT",
                            dt1,
                            req,
                            string.Empty,
                            string.Empty,
                            bodyData.Length.ToString(),
                            ""));
                    using (Stream postStream = new MemoryStream())
                    {
                        postStream.Write(bodyData, 0, bodyData.Length);
                        postStream.Seek(0, SeekOrigin.Begin);
                        StreamContent contentStream = new StreamContent(postStream);
                        req.Content = contentStream;

                        using (HttpResponseMessage response = await client.SendAsync(req, ct))
                        {
                            this.log.LogMessage(
                                "Received response to combine block list for file {0}: Status Code:{1} Status Desc: {2}",
                                fileName,
                                response.StatusCode,
                                await response.Content.ReadAsStringAsync());
                        }
                    }
                }
            }
        }
    }
}