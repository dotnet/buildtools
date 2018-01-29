// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public class CopyBlobsToLatest : AzureConnectionStringBuildTask
    {
        [Required]
        public string ContainerName { get; set; }

        [Required]
        public string Product { get; set; }

        [Required]
        public string ProductVersion { get; set; }

        [Required]
        public string Channel { get; set; }

        [Required]
        public string Commit { get; set; }

        public bool Coherent { get; set; }

        /// <summary>
        /// A list of full version strings that should be converted to "latest" for each blob id,
        /// in addition to ProductVersion.
        /// </summary>
        public string[] FullVersions { get; set; }

        /// <summary>
        /// Forces publish even if the Commit was already published.
        /// </summary>
        public bool ForcePublish { get; set; }

        private Regex _versionRegex = new Regex(@"(?<version>\d+\.\d+\.\d+)(-(?<prerelease>[^-]+-)?(?<major>\d+)-(?<minor>\d+))?");

        public override bool Execute()
        {
            ParseConnectionString();

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            string sourceDir = $"{Product}/{ProductVersion}/";
            string channelDir = $"{Product}/{Channel}/";
            string semaphoreBlob = $"{channelDir}publishSemaphore";

            CreateBlobIfNotExists(semaphoreBlob);

            AzureBlobLease blobLease = new AzureBlobLease(
                AccountName,
                AccountKey,
                ConnectionString,
                ContainerName,
                semaphoreBlob,
                Log);

            Log.LogMessage($"Acquiring lease on semaphore blob '{semaphoreBlob}'");
            blobLease.Acquire();

            try
            {
                string targetVersionFile = $"{channelDir}{ProductVersion}";

                // Prevent race conditions by dropping a version hint of what version this is. If
                // we see this file and it is the same as our version then we know that a race
                // happened where two+ builds finished at the same time and someone already took
                // care of publishing and we have no work to do.
                if (IsLatestSpecifiedVersion(targetVersionFile) && !ForcePublish)
                {
                    Log.LogMessage(
                        MessageImportance.High,
                        $"Version '{ProductVersion}' is already published, skipping finalization. " +
                            $"Hint file: '{targetVersionFile}'");

                    return true;
                }

                // Delete old version files
                GetBlobList(channelDir)
                    .Select(s => s.Replace($"/{ContainerName}/", ""))
                    .Where(w => _versionRegex.Replace(Path.GetFileName(w), "") == "")
                    .ToList()
                    .ForEach(f => TryDeleteBlob(f));

                // Drop the version file signaling such for any race-condition builds (see above comment).
                CreateBlobIfNotExists(targetVersionFile);

                CopyBlobs(sourceDir, channelDir);

                // Generate the latest version text file
                string versionText =
                    $"{Commit}{Environment.NewLine}" +
                    $"{ProductVersion}{Environment.NewLine}";

                PublishStringToBlob(
                    ContainerName,
                    $"{channelDir}latest.version",
                    versionText,
                    "text/plain");

                if (Coherent)
                {
                    PublishStringToBlob(
                        ContainerName,
                        $"{channelDir}latest.coherent.version",
                        versionText,
                        "text/plain");
                }
            }
            finally
            {
                Log.LogMessage($"Releasing lease on semaphore blob '{semaphoreBlob}'");
                blobLease.Release();
            }

            return !Log.HasLoggedErrors;
        }

        public bool CopyBlobs(string sourceFolder, string destinationFolder)
        {
            // List of versions that need to be replaced with "latest" when copying blobs.
            var versions = new List<string> { ProductVersion };
            if (FullVersions != null)
            {
                versions.AddRange(FullVersions);
            }

            bool returnStatus = true;
            List<Task<bool>> copyTasks = new List<Task<bool>>();
            string[] blobs = GetBlobList(sourceFolder);
            foreach (string blob in blobs)
            {
                string targetName = versions.Aggregate(
                    Path.GetFileName(blob),
                    (agg, version) => agg.Replace(version, "latest"));

                string sourceBlob = blob.Replace($"/{ContainerName}/", "");
                string destinationBlob = $"{destinationFolder}{targetName}";
                Log.LogMessage($"Copying blob '{sourceBlob}' to '{destinationBlob}'");
                copyTasks.Add(CopyBlobAsync(sourceBlob, destinationBlob));
            }
            Task.WaitAll(copyTasks.ToArray());
            copyTasks.ForEach(c => returnStatus &= c.Result);
            return returnStatus;
        }

        public bool TryDeleteBlob(string path)
        {
            return DeleteBlob(ContainerName, path);
        }

        public void CreateBlobIfNotExists(string path)
        {
            var blobList = GetBlobList(path);
            if (blobList.Count() == 0)
            {
                PublishStringToBlob(ContainerName, path, DateTime.Now.ToString());
            }
        }

        public bool IsLatestSpecifiedVersion(string versionFile)
        {
            var blobList = GetBlobList(versionFile);
            return blobList.Count() != 0;
        }

        public bool DeleteBlob(string container, string blob)
        {
            return DeleteAzureBlob.Execute(
                AccountName,
                AccountKey,
                ConnectionString,
                container,
                blob,
                BuildEngine,
                HostObject);
        }

        public Task<bool> CopyBlobAsync(string sourceBlobName, string destinationBlobName)
        {
            return CopyAzureBlobToBlob.ExecuteAsync(
                AccountName,
                AccountKey,
                ConnectionString,
                ContainerName,
                sourceBlobName,
                destinationBlobName,
                BuildEngine,
                HostObject);
        }

        public string[] GetBlobList(string path)
        {
            return ListAzureBlobs.Execute(
                AccountName,
                AccountKey,
                ConnectionString,
                ContainerName,
                path,
                BuildEngine,
                HostObject);
        }

        public bool PublishStringToBlob(string container, string blob, string contents, string contentType = null)
        {
            return PublishStringToAzureBlob.Execute(
                AccountName,
                AccountKey,
                ConnectionString,
                container,
                blob,
                contents,
                contentType,
                BuildEngine,
                HostObject);
        }
    }
}
