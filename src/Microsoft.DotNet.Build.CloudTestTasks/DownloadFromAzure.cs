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

        public ITaskItem[] BlobNames { get; set; }
        
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

            try
            {
                List<string> blobNames = new List<string>();
                if (BlobNames == null)
                {
                    ListAzureBlobs listAzureBlobs = new ListAzureBlobs()
                    {
                        AccountName = AccountName,
                        AccountKey = AccountKey,
                        ContainerName = ContainerName,
                        FilterBlobNames = BlobNamePrefix,
                        BuildEngine = this.BuildEngine,
                        HostObject = this.HostObject
                    };                    
                    listAzureBlobs.Execute();
                    blobNames = listAzureBlobs.BlobNames.ToList();
                }
                else
                {
                    blobNames = BlobNames.Select(b => b.ItemSpec).ToList<string>();
                    if (BlobNamePrefix != null)
                    {
                        blobNames = blobNames.Where(b => b.StartsWith(BlobNamePrefix)).ToList<string>();
                    }
                }
                // track the number of blobs that fail to download
                int failureCount = 0;
                using (HttpClient client = new HttpClient())
                {
                    foreach (string blob in blobNames)
                    {
                        Log.LogMessage(MessageImportance.Low, "Downloading BLOB - {0}", blob);
                        string urlGetBlob = AzureHelper.GetBlobRestUrl(AccountName, ContainerName, blob);

                        int dirIndex = blob.LastIndexOf("/");
                        string blobDirectory = string.Empty;
                        string blobFilename = string.Empty;

                        if (dirIndex == -1)
                        {
                            blobFilename = blob;
                        }
                        else
                        {
                            blobDirectory = blob.Substring(0, dirIndex);
                            blobFilename = blob.Substring(dirIndex + 1);

                            // Trim blob name prefix (directory part) from download to blob directory
                            if(BlobNamePrefix != null)
                            {
                                if(BlobNamePrefix.Length > dirIndex)
                                {
                                    BlobNamePrefix = BlobNamePrefix.Substring(0, dirIndex);
                                }
                                blobDirectory = blobDirectory.Substring(BlobNamePrefix.Length);
                            }
                        }
                        string downloadBlobDirectory = Path.Combine(DownloadDirectory, blobDirectory);
                        if (!Directory.Exists(downloadBlobDirectory))
                        {
                            Directory.CreateDirectory(downloadBlobDirectory);
                        }
                        string filename = Path.Combine(downloadBlobDirectory, blobFilename);

                        var createRequest = AzureHelper.RequestMessage("GET", urlGetBlob, AccountName, AccountKey);

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
