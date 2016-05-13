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

using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class DownloadFromAzure : Task
    {
        /// <summary>
        /// The Azure account name used when creating the connection string.
        /// </summary>
        [Required]
        public string AccountName { get; set; }

        /// <summary>
        /// The Azure account key used when creating the connection string.
        /// </summary>
        [Required]
        public string AccountKey { get; set; }

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

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            Log.LogMessage(MessageImportance.Normal, "Downloading contents of container {0} from storage account '{1}' to directory {2}.",
                ContainerName, AccountName, DownloadDirectory);

            DateTime dateTime = DateTime.UtcNow;
            List<string> blobsNames = null;
            string urlListBlobs = string.Format("https://{0}.blob.core.windows.net/{1}?restype=container&comp=list", AccountName, ContainerName);

            Log.LogMessage(MessageImportance.Low, "Sending request to list blobsNames for container '{0}'.", ContainerName);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Func<HttpRequestMessage> createRequest = () =>
                    {
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
                    using (HttpResponseMessage response = await AzureHelper.RequestWithRetry(Log, client, createRequest))
                    {
                        responseFile = new XmlDocument();
                        responseFile.LoadXml(await response.Content.ReadAsStringAsync());
                        XmlNodeList elemList = responseFile.GetElementsByTagName("Name");

                        blobsNames = elemList.Cast<XmlNode>()
                                                    .Select(x => x.InnerText)
                                                    .ToList();

                        if (blobsNames.Count == 0)
                            Log.LogWarning("No blobs were found.");
                    }

                    // track the number of blobs that fail to download
                    int failureCount = 0;

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
                                Stream responseStream = await response.Content.ReadAsStreamAsync();
                                using (FileStream sourceStream = File.Open(filename, FileMode.Create))
                                {
                                    responseStream.CopyTo(sourceStream);
                                }
                            }
                            else
                            {
                                Log.LogError("Failed to retrieve blob {0}, the status code was {1}", blob, response.StatusCode);
                                ++failureCount;
                            }
                        }
                    }

                    // if no blobs failed to download the task succeeded
                    return (failureCount == 0);
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e, true);
                    return false;
                }
            }
        }
    }
}
