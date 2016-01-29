// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class CreateAzureFileShare : Task
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
        /// The read-only SAS token created when ReadOnlyTokenDaysValid is greater than zero.
        /// </summary>
        [Output]
        public string ReadOnlyToken { get; set; }

        /// <summary>
        /// The number of days for which the read-only token should be valid.
        /// </summary>
        public int ReadOnlyTokenDaysValid { get; set; }

        /// <summary>
        /// The name of the share to create.  The specified name must be in the correct format.
        /// </summary>
        [Required]
        public string ShareName { get; set; }

        /// <summary>
        /// The URI of the created file share.
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

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "Creating file share named '{0}' in storage account {1}.", ShareName, AccountName);

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", AccountName, AccountKey));
            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare fileShare = fileClient.GetShareReference(ShareName);
            fileShare.CreateIfNotExists();
            StorageUri = fileShare.Uri.ToString();

            // NOTE: by convention the tokens don't contain the leading '?' character.

            if (ReadOnlyTokenDaysValid > 0)
            {
                SharedAccessFilePolicy rFilePolicy = new SharedAccessFilePolicy();
                rFilePolicy.SharedAccessExpiryTime = DateTime.UtcNow.AddDays(ReadOnlyTokenDaysValid);
                rFilePolicy.Permissions = SharedAccessFilePermissions.Read;
                ReadOnlyToken = fileShare.GetSharedAccessSignature(rFilePolicy).Substring(1);
            }

            if (WriteOnlyTokenDaysValid > 0)
            {
                SharedAccessFilePolicy wFilePolicy = new SharedAccessFilePolicy();
                wFilePolicy.SharedAccessExpiryTime = DateTime.UtcNow.AddDays(WriteOnlyTokenDaysValid);
                wFilePolicy.Permissions = SharedAccessFilePermissions.Read | SharedAccessFilePermissions.Write | SharedAccessFilePermissions.List | SharedAccessFilePermissions.Delete;
                WriteOnlyToken = fileShare.GetSharedAccessSignature(wFilePolicy).Substring(1);
            }

            return true;
        }
    }
}
