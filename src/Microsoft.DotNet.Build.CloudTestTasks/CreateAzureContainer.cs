// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class CreateAzureContainer : Task
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
        /// The name of the container to create.  The specified name must be in the correct format, see the
        /// following page for more info.  https://msdn.microsoft.com/en-us/library/azure/dd135715.aspx
        /// </summary>
        [Required]
        public string ContainerName { get; set; }

        /// <summary>
        /// The read-only SAS token created when ReadOnlyTokenDaysValid is greater than zero.
        /// </summary>
        [Output]
        public string ReadOnlyToken { get; set; }

        /// <summary>
        /// The number of days for which the read-only token should be valid.
        /// </summary>
        public int ReadOnlyTokenDaysValid { get; set; }

        /// <summary>
        /// The URI of the created container.
        /// </summary>
        [Output]
        public string StorageUri { get; set; }

        /// <summary>
        /// The write-only SAS token create when WriteOnlyTokenDaysValid is greater than zero.
        /// </summary>
        [Output]
        public string WriteOnlyToken { get; set; }

        /// <summary>
        /// The number of days for which the write-only token should be valid.
        /// </summary>
        public int WriteOnlyTokenDaysValid { get; set; }

        /// <summary>
        /// When false, if the specified container already exists get a reference to it.
        /// When true, if the specified container already exists the task will fail.
        /// </summary>
        public bool FailIfExists { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "Creating container named '{0}' in storage account {1}.", ContainerName, AccountName);

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", AccountName, AccountKey));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer newContainer = blobClient.GetContainerReference(ContainerName);

            if (FailIfExists && newContainer.Exists())
            {
                Log.LogError("The container '{0}' already exists.", ContainerName);
                return false;
            }
            else
            {
                newContainer.CreateIfNotExists();
            }

            StorageUri = newContainer.Uri.ToString();

            if (ReadOnlyTokenDaysValid > 0)
            {
                var sasRO = new SharedAccessBlobPolicy();
                sasRO.SharedAccessExpiryTime = DateTime.UtcNow.AddDays(ReadOnlyTokenDaysValid);
                sasRO.Permissions = SharedAccessBlobPermissions.Read;
                ReadOnlyToken = newContainer.GetSharedAccessSignature(sasRO);
            }

            if (WriteOnlyTokenDaysValid > 0)
            {
                var sasWO = new SharedAccessBlobPolicy();
                sasWO.SharedAccessExpiryTime = DateTime.UtcNow.AddDays(WriteOnlyTokenDaysValid);
                sasWO.Permissions = SharedAccessBlobPermissions.Write;
                WriteOnlyToken = newContainer.GetSharedAccessSignature(sasWO);
            }

            return true;
        }
    }
}
