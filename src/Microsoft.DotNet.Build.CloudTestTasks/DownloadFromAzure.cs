// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
    
namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class DownloadFromAzure : AzureConnectionStringBuildTask
    {
        /// <summary>
        /// The name of the container to access.  The specified name must be in the correct format, see the
        /// following page for more info.  https://msdn.microsoft.com/en-us/library/azure/dd135715.aspx
        /// </summary>
        [Required]
        public string ContainerName { get; set; }

        /// <summary>
        /// Directory to download blob files to.
        /// </summary>
        [Required]
        public string DownloadDirectory { get; set; }

        public string BlobNamePrefix { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            ParseConnectionString();
            // If the connection string AND AccountKey & AccountName are provided, error out.
            if (Log.HasLoggedErrors)
            {
                return false;
            }

            Log.LogMessage(MessageImportance.Normal, "Downloading contents of container {0} from storage account '{1}' to directory {2}.",
                ContainerName, AccountName, DownloadDirectory);

            string blobprefix = string.IsNullOrEmpty(BlobNamePrefix) ? "" : "&prefix=" + BlobNamePrefix;

            List<string> blobsNames = new List<string>();
            string urlListBlobs = string.Format("https://{0}.blob.core.windows.net/{1}?restype=container&comp=list{2}", AccountName, ContainerName, blobprefix);

            Log.LogMessage(MessageImportance.Low, "Sending request to list blobsNames for container '{0}'.", ContainerName);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Func<HttpRequestMessage> createRequest = () =>
                    {
                        DateTime dateTime = DateTime.UtcNow;
                        var request = new HttpRequestMessage(HttpMethod.Get, urlListBlobs);
                        request.Headers.Add(AzureHelper.DateHeaderString, dateTime.ToString("R", CultureInfo.InvariantCulture));
                        request.Headers.Add(AzureHelper.VersionHeaderString, AzureHelper.StorageApiVersion);
                        request.Headers.Add(AzureHelper.AuthorizationHeaderString, AzureHelper.AuthorizationHeader(
                                AccountName,
                                AccountKey,
                                "GET",
                                dateTime,
                                request));
                        return request;
                    };

                    XmlDocument responseFile;
                    string nextMarker = string.Empty;
                    using (HttpResponseMessage response = await AzureHelper.RequestWithRetry(Log, client, createRequest))
                    {
                        responseFile = new XmlDocument();
                        responseFile.LoadXml(await response.Content.ReadAsStringAsync());
                        XmlNodeList elemList = responseFile.GetElementsByTagName("Name");

                        blobsNames.AddRange(elemList.Cast<XmlNode>()
                                                    .Select(x => x.InnerText)
                                                    .ToList());
                        nextMarker = responseFile.GetElementsByTagName("NextMarker").Cast<XmlNode>().FirstOrDefault()?.InnerText;
                    }
                    while (!string.IsNullOrEmpty(nextMarker))
                    {
                        urlListBlobs = string.Format($"https://{AccountName}.blob.core.windows.net/{ContainerName}?restype=container&comp=list&marker={nextMarker}");
                        using (HttpResponseMessage response = AzureHelper.RequestWithRetry(Log, client, createRequest).GetAwaiter().GetResult())
                        {
                            responseFile = new XmlDocument();
                            responseFile.LoadXml(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                            XmlNodeList elemList = responseFile.GetElementsByTagName("Name");

                            blobsNames.AddRange(elemList.Cast<XmlNode>()
                                                        .Select(x => x.InnerText)
                                                        .ToList());

                            nextMarker = responseFile.GetElementsByTagName("NextMarker").Cast<XmlNode>().FirstOrDefault()?.InnerText;
                        }
                    }

                    // track the number of blobs that fail to download
                    int failureCount = 0;
                    if (blobsNames.Count == 0)
                        Log.LogWarning("No blobs were found.");
                    else
                        Log.LogMessage(MessageImportance.Low, $"{blobsNames.Count} blobs found.");

                    foreach (string blob in blobsNames)
                    {
                        Log.LogMessage(MessageImportance.Low, "Downloading BLOB - {0}", blob);
                        string urlGetBlob = string.Format("https://{0}.blob.core.windows.net/{1}/{2}", AccountName, ContainerName, blob);

                        string filename = Path.Combine(DownloadDirectory, blob);
                        string blobDirectory = blob.Substring(0, blob.LastIndexOf("/"));
                        string downloadBlobDirectory = Path.Combine(DownloadDirectory, blobDirectory);
                        if (!Directory.Exists(downloadBlobDirectory))
                        {
                            Directory.CreateDirectory(downloadBlobDirectory);
                        }

                        createRequest = () =>
                        {
                            DateTime dateTime = DateTime.UtcNow;
                            var request = new HttpRequestMessage(HttpMethod.Get, urlGetBlob);
                            request.Headers.Add(AzureHelper.DateHeaderString, dateTime.ToString("R", CultureInfo.InvariantCulture));
                            request.Headers.Add(AzureHelper.VersionHeaderString, AzureHelper.StorageApiVersion);
                            request.Headers.Add(AzureHelper.AuthorizationHeaderString, AzureHelper.AuthorizationHeader(
                                    AccountName,
                                    AccountKey,
                                    "GET",
                                    dateTime,
                                    request));
                            return request;
                        };

                        using (HttpResponseMessage response = await AzureHelper.RequestWithRetry(Log, client, createRequest))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                // Blobs can be files but have the name of a directory.  We'll skip those and log something weird happened.
                                if (!string.IsNullOrEmpty(Path.GetFileName(filename)))
                                {
                                    Stream responseStream = await response.Content.ReadAsStreamAsync();

                                    using (FileStream sourceStream = File.Open(filename, FileMode.Create))
                                    {
                                        responseStream.CopyTo(sourceStream);
                                    }
                                }
                                else
                                {
                                    Log.LogWarning($"Unable to download blob '{blob}' as it has a directory-like name.  This may cause problems if it was needed.");
                                }
                            }
                            else
                            {
                                Log.LogError("Failed to retrieve blob {0}, the status code was {1}", blob, response.StatusCode);
                                ++failureCount;
                            }
                        }
                    }
                    Log.LogMessage($"{failureCount} errors seen downloading blobs.");
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e, true);
                }
                return !Log.HasLoggedErrors;
            }
        }
    }
}
