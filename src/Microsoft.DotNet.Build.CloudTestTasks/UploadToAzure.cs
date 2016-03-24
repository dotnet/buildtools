// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Linq;
using System.Net.Http;

using Microsoft.Build.Framework;

using Task = Microsoft.Build.Utilities.Task;
using ThreadingTask = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Build.CloudTestTasks
{

    public class UploadToAzure : Task, ICancelableTask
    {
        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;

        /// <summary>
        ///     The Azure account key used when creating the connection string.
        /// </summary>
        [Required]
        public string AccountKey { get; set; }

        /// <summary>
        ///     The Azure account name used when creating the connection string.
        /// </summary>
        [Required]
        public string AccountName { get; set; }

        /// <summary>
        ///     The name of the container to access.  The specified name must be in the correct format, see the
        ///     following page for more info.  https://msdn.microsoft.com/en-us/library/azure/dd135715.aspx
        /// </summary>
        [Required]
        public string ContainerName { get; set; }

        /// <summary>
        ///     An item group of files to upload.  Each item must have metadata RelativeBlobPath
        ///     that specifies the path relative to ContainerName where the item will be uploaded.
        /// </summary>
        [Required]
        public ITaskItem[] Items { get; set; }

        /// <summary>
        ///     Indicates if the destination blob should be overwritten if it already exists.  The default if false.
        /// </summary>
        public bool Overwrite { get; set; } = false;

        public void Cancel()
        {
            TokenSource.Cancel();
        }

        public override bool Execute()
        {
            return ExecuteAsync(CancellationToken).GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync(CancellationToken ct)
        {
            Log.LogMessage(
                MessageImportance.High, 
                "Begin uploading blobs to Azure account {0} in container {1}.", 
                AccountName, 
                ContainerName);

            if (Items.Length == 0)
            {
                throw new ArgumentException("No items were provided for upload.");
            }

            // first check what blobs are present
            string checkListUrl = string.Format(
                "https://{0}.blob.core.windows.net/{1}?restype=container&comp=list", 
                AccountName, 
                ContainerName);

            DateTime dt = DateTime.UtcNow;
            HashSet<string> blobsPresent = new HashSet<string>();

            using (HttpClient client = new HttpClient())
            {
                using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, checkListUrl))
                {
                    req.Headers.Add(AzureHelper.DateHeaderString, dt.ToString("R", CultureInfo.InvariantCulture));
                    req.Headers.Add(AzureHelper.VersionHeaderString, AzureHelper.StorageApiVersion);
                    req.Headers.Add(AzureHelper.AuthorizationHeaderString, AzureHelper.AuthorizationHeader(
                        AccountName,
                        AccountKey,
                        "GET",
                        dt,
                        req));
                    
                    Log.LogMessage(MessageImportance.Normal, "Sending request to check whether Container blobs exist");
                    XmlDocument doc;
                    using (HttpResponseMessage response = await client.SendAsync(req, ct))
                    {
                        doc = new XmlDocument();
                        doc.LoadXml(await response.Content.ReadAsStringAsync());
                    }

                    XmlNodeList nodes = doc.DocumentElement.GetElementsByTagName("Blob");

                    foreach (XmlNode node in nodes)
                    {
                        blobsPresent.Add(node["Name"].InnerText);
                    }

                    Log.LogMessage(MessageImportance.Normal, "Received response to check whether Container blobs exist");
                }
            }
            await ThreadingTask.WhenAll(Items.Select(item => UploadAsync(ct, item, blobsPresent)));

           Log.LogMessage(MessageImportance.High, "Upload to Azure is complete, a total of {0} items were uploaded.", Items.Length);

            return true;
        }

        private async ThreadingTask UploadAsync(CancellationToken ct, ITaskItem item, HashSet<string> blobsPresent)
        {
            bool result = true;
            if (ct.IsCancellationRequested)
            {
                Log.LogError("Task UploadToAzure cancelled");
                ct.ThrowIfCancellationRequested();
            }

            string relativeBlobPath = item.GetMetadata("RelativeBlobPath");
            if (string.IsNullOrEmpty(relativeBlobPath))
            {
                Log.LogError(string.Format("Metadata 'RelativeBlobPath' is missing for item '{0}'.", item.ItemSpec));
                result = false;
            }

            if (!File.Exists(item.ItemSpec))
            {
                Log.LogError(string.Format("The file '{0}' does not exist.", item.ItemSpec));
                result = false;
            }

            if (!Overwrite && blobsPresent.Contains(relativeBlobPath))
            {
                Log.LogError(string.Format("The blob '{0}' already exists.", relativeBlobPath));
                result = false;
            }

            if (result)
            {
                Log.LogMessage("Uploading {0} to {1}.", item.ItemSpec, ContainerName);
                UploadClient uploadClient = new UploadClient(Log);
                await
                    uploadClient.UploadBlockBlobAsync(
                        ct,
                        AccountName,
                        AccountKey,
                        ContainerName,
                        item.ItemSpec,
                        relativeBlobPath);
            }
        }
    }
}