// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using MSBuild = Microsoft.Build.Utilities;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Microsoft.DotNet.Build.CloudTestTasks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    sealed class BlobFeedAction
    {
        private MSBuild.TaskLoggingHelper Log;
        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;

        public BlobFeed feed;
        public int MaxClients { get; set; } = 8;

        public BlobFeedAction(string accountName, string accountKey, string containerName, string indexDirectory, MSBuild.TaskLoggingHelper Log)
        {
            this.feed = new BlobFeed(accountName, accountKey, containerName, string.IsNullOrWhiteSpace(indexDirectory) ? Path.GetTempPath() : indexDirectory, Log);
            this.Log = Log;
        }

        public async Task<bool> PushToFeed(IEnumerable<string> items, string relativePath, bool allowOverwrite = false)
        {
            if (feed.IsSanityChecked(items))
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    Log.LogError("Task PushToFeed cancelled");
                    CancellationToken.ThrowIfCancellationRequested();
                }

                using (var clientThrottle = new SemaphoreSlim(this.MaxClients, this.MaxClients))
                {
                    await Task.WhenAll(items.Select(item => PushItemToFeed(item, relativePath, clientThrottle, allowOverwrite)));
                }
            }

            return !Log.HasLoggedErrors;
        }

        public async Task<bool> PushToFeedFlat(IEnumerable<string> items, string relativePath, bool allowOverwrite = false)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                Log.LogError("Task PushToFeedFlat cancelled");
                CancellationToken.ThrowIfCancellationRequested();
            }

            using (var clientThrottle = new SemaphoreSlim(this.MaxClients, this.MaxClients))
            {
                await Task.WhenAll(items.Select(item => PushItemToFeed(item, relativePath, clientThrottle, allowOverwrite)));
            }
            return !Log.HasLoggedErrors;
        }

        public async Task<bool> PushItemToFeed(string item, string relativePath, SemaphoreSlim clientThrottle, bool allowOverwrite)
        {
            try
            {
                string uploadPath = feed.CalculateBlobPath(item, relativePath);
                string packageDirectory = feed.CalculateRelativeUploadPath(item, relativePath);

                if (await feed.CheckIfBlobExists(uploadPath) && !allowOverwrite)
                {
                    throw new Exception($"Item {uploadPath} already exists and Overwrite is false.");
                }
                await UploadAsync(CancellationToken, item, uploadPath, clientThrottle, allowOverwrite);

                List<string> listAzureBlobs = await ListAzureBlobs.ListBlobs(Log, feed.AccountName, feed.AccountKey, feed.ContainerName, packageDirectory);
                if (!listAzureBlobs.Any(x => x.Contains(uploadPath)))
                {
                    throw new Exception($"Uploaded package {uploadPath} is not present on feed. Cannot update index.json.");
                }

                await UploadIndexJson(clientThrottle, true, packageDirectory, listAzureBlobs);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        private async Task UploadIndexJson(SemaphoreSlim clientThrottle, bool allowOverwrite, string packageDirectory, List<string> listAzureBlobs)
        {
            listAzureBlobs.Remove(listAzureBlobs.Find(x => x.Contains("index.json")));
            List<string> updatedVersions = new List<string>();
            foreach (var version in listAzureBlobs)
            {
                string versionToCheck = version.Substring(packageDirectory.Length + 1).Split('/')[0];
                NuGetVersion nugetVersion = null;
                if (NuGetVersion.TryParse(versionToCheck, out nugetVersion))
                {
                    updatedVersions.Add(versionToCheck);
                }
            }
            string packageIndexJsonLocation = feed.GeneratePackageServiceIndex(packageDirectory, updatedVersions);
            if (!await feed.CheckIfBlobExists(packageIndexJsonLocation))
            {
                allowOverwrite = false;
            }
            await UploadAsync(CancellationToken, packageIndexJsonLocation, packageDirectory + "/index.json", clientThrottle, allowOverwrite);
        }

        private async Task UploadAsync(CancellationToken ct, string item, string uploadPath, SemaphoreSlim clientThrottle, bool isLeaseRequired)
        {
            if (!File.Exists(item))
                throw new Exception(string.Format("The file '{0}' does not exist.", item));

            await clientThrottle.WaitAsync();
            string leaseId = string.Empty;
            AzureBlobLease blobLease = new AzureBlobLease(feed.AccountName, feed.AccountKey, string.Empty, feed.ContainerName, uploadPath, Log, "60", "10");

            if (isLeaseRequired)
            {
                try
                {
                    leaseId = blobLease.Acquire();
                }
                catch (Exception)
                {
                    Log.LogError($"Unable to obtain lease on {uploadPath}");
                }
            }
            try
            {
                Log.LogMessage($"Uploading {item} to {uploadPath}.");
                UploadClient uploadClient = new UploadClient(Log);
                await
                    uploadClient.UploadBlockBlobAsync(
                        ct,
                        feed.AccountName,
                        feed.AccountKey,
                        feed.ContainerName,
                        item,
                        uploadPath,
                        leaseId);
            }
            catch (Exception)
            {
                Log.LogError($"Unable to upload to {uploadPath}");
            }
            finally
            {
                if (isLeaseRequired)
                {
                    blobLease.Release();
                }
                clientThrottle.Release();
            }
        }
    }
}
