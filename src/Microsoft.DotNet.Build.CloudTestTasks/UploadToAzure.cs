// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class UploadToAzure : Task
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
        /// An item group of files to upload.  Each item must have metadata RelativeBlobPath
        /// that specifies the path relative to ContainerName where the item will be uploaded.
        /// </summary>
        [Required]
        public ITaskItem[] Items { get; set; }

        /// <summary>
        /// Indicates if the destination blob should be overwritten if it already exists.  The default if false.
        /// </summary>
        public bool Overwrite { get; set; }

        public UploadToAzure()
        {
            Overwrite = false;
        }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "Begin uploading blobs to Azure account {0} in container {1}.", AccountName, ContainerName);

            if (Items.Length == 0)
                throw new ArgumentException("No items were provided for upload.");

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", AccountName, AccountKey));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);

            bool result = true;
            System.Threading.Tasks.Parallel.ForEach(Items, (item, loopState) =>
            {
                var relativeBlobPath = item.GetMetadata("RelativeBlobPath");
                if (string.IsNullOrEmpty(relativeBlobPath))
                {
                    Log.LogError(string.Format("Metadata 'RelativeBlobPath' is missing for item '{0}'.", item.ItemSpec));
                    result = false;
                    loopState.Stop();
                }

                CloudBlockBlob blockBlob = container.GetBlockBlobReference(relativeBlobPath);

                if (!Overwrite && blockBlob.Exists())
                {
                    Log.LogError(string.Format("The blob '{0}' already exists.", blockBlob.Uri));
                    result = false;
                    loopState.Stop();
                }

                using (var fileStream = File.OpenRead(item.ItemSpec))
                {
                    Log.LogMessage(MessageImportance.Low, "Uploading {0} to {1}.", item.ItemSpec, blockBlob.Uri);
                    blockBlob.UploadFromStream(fileStream);
                }
            });

            if (result)
                Log.LogMessage(MessageImportance.High, "Upload to Azure is complete, a total of {0} items were uploaded.", Items.Length);

            return result;
        }
    }
}
