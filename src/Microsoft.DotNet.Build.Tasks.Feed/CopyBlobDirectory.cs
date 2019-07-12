// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public sealed class CopyBlobDirectory : BuildTask
    {
        [Required]
        public string SourceBlobDirectory { get; set; }

        [Required]
        public string TargetBlobDirectory { get; set; }

        public string AccountKey { get; set; }

        public string SourceAccountKey { get; set; }

        public string TargetAccountKey { get; set; }

        public bool Overwrite { get; set; }

        public bool SkipCreateContainer { get; set; } = false;

        public bool SkipIfMissing { get; set; } = false;

        public int CopyWaitTimeoutInMinutes { get; set; } = 120;

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private static string GetCanonicalStorageUri(string uri, string subPath = null)
        {
            string newUri = uri.TrimEnd('/');
            if (!string.IsNullOrEmpty(subPath))
            {
                newUri = $"{newUri}/{subPath.Trim('/')}";
            }
            return newUri;
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Log.LogMessage("Performing blob merge...");

                string sourceKey = SourceAccountKey ?? AccountKey;
                string targetKey = TargetAccountKey ?? AccountKey;

                if (string.IsNullOrEmpty(SourceBlobDirectory) ||
                    string.IsNullOrEmpty(TargetBlobDirectory) ||
                    string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(targetKey))
                {
                    Log.LogError($"Please specify a source blob directory, a target blob directory and account keys");
                }
                else
                {
                    // Canonicalize the target uri
                    string targetUri = GetCanonicalStorageUri(TargetBlobDirectory);
                    // Invoke the blob URI parser on the target URI and deal with any container creation that needs to happen
                    BlobUrlInfo targetUrlInfo = new BlobUrlInfo(targetUri);
                    CloudStorageAccount targetStorageAccount = new CloudStorageAccount(new WindowsAzure.Storage.Auth.StorageCredentials(targetUrlInfo.AccountName, targetKey), true);
                    CloudBlobClient targetClient = targetStorageAccount.CreateCloudBlobClient();
                    CloudBlobContainer targetContainer = targetClient.GetContainerReference(targetUrlInfo.ContainerName);

                    if (!SkipCreateContainer)
                    {
                        Log.LogMessage($"Creating container {targetUrlInfo.ContainerName} if it doesn't exist.");
                        await targetContainer.CreateIfNotExistsAsync();
                    }

                    string sourceUri = GetCanonicalStorageUri(SourceBlobDirectory);
                    // Grab the source blob path from the source info and combine with the target blob path.
                    BlobUrlInfo sourceBlobInfo = new BlobUrlInfo(sourceUri);
                    CloudStorageAccount sourceStorageAccount = new CloudStorageAccount(new WindowsAzure.Storage.Auth.StorageCredentials(sourceBlobInfo.AccountName, sourceKey), true);
                    CloudBlobClient sourceClient = sourceStorageAccount.CreateCloudBlobClient();

                    CloudBlobContainer sourceContainer = sourceClient.GetContainerReference(sourceBlobInfo.ContainerName);

                    Log.LogMessage($"Listing blobs in {sourceUri}");

                    // Get all source URI's with the blob prefix
                    BlobContinuationToken token = null;
                    List<IListBlobItem> sourceBlobs = new List<IListBlobItem>();
                    do
                    {
                        BlobResultSegment segment = await sourceContainer.ListBlobsSegmentedAsync(sourceBlobInfo.BlobPath, true,
                            BlobListingDetails.None, null, token, new BlobRequestOptions(), null);
                        token = segment.ContinuationToken;
                        sourceBlobs.AddRange(segment.Results);
                    }
                    while (token != null);

                    // Ensure the source exists
                    if (!SkipIfMissing && sourceBlobs.Count == 0)
                    {
                        Log.LogError($"No blobs found in {sourceUri}");
                    }

                    await Task.WhenAll(sourceBlobs.Select(async blob =>
                    {
                        // Determine the relative URI for the target.  This works properly when the
                        // trailing slash is left off of the source and target URIs.
                        string relativeBlobPath = blob.Uri.ToString().Substring(sourceUri.Length);
                        string specificTargetUri = GetCanonicalStorageUri(targetUri, relativeBlobPath);
                        BlobUrlInfo specificTargetBlobUrlInfo = new BlobUrlInfo(specificTargetUri);
                        CloudBlob targetBlob = targetContainer.GetBlobReference(specificTargetBlobUrlInfo.BlobPath);

                        Log.LogMessage($"Merging {blob.Uri.ToString()} into {targetBlob.Uri.ToString()}");

                        if (!Overwrite && await targetBlob.ExistsAsync())
                        {
                            Log.LogError($"Target blob {targetBlob.Uri.ToString()} already exists.");
                        }

                        BlobUrlInfo specificSourceBlobUrlInfo = new BlobUrlInfo(blob.Uri);
                        CloudBlob sourceBlob = sourceContainer.GetBlobReference(specificSourceBlobUrlInfo.BlobPath);
                        SharedAccessBlobPolicy policy = new SharedAccessBlobPolicy
                        {
                            Permissions = SharedAccessBlobPermissions.Read,
                            SharedAccessStartTime = null,
                            SharedAccessExpiryTime = DateTimeOffset.Now.AddMinutes(30)
                        };
                        string sas = sourceBlob.GetSharedAccessSignature(policy);
                        await targetBlob.StartCopyAsync(new Uri(blob.Uri + sas));

                        DateTime endWaitTime = DateTime.Now.AddMinutes(CopyWaitTimeoutInMinutes);
                        TimeSpan waitInterval = TimeSpan.FromSeconds(30);
                        ICloudBlob copyInProgessBlob;
                        do
                        {
                            await Task.Delay(waitInterval);
                            copyInProgessBlob = await targetContainer.GetBlobReferenceFromServerAsync(specificTargetBlobUrlInfo.BlobPath);
                        }
                        while (DateTime.Now.CompareTo(endWaitTime) < 0 && copyInProgessBlob.CopyState.Status == CopyStatus.Pending);

                        if (copyInProgessBlob?.CopyState?.Status != CopyStatus.Success)
                        {
                            Log.LogError($"{copyInProgessBlob.Uri.ToString()} timed out or failed.");
                        }
                        else
                        {
                            Log.LogMessage($"{copyInProgessBlob.Uri.ToString()} completed.");
                        }
                    }));
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
