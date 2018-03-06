// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public sealed class ParseBlobUrl : BuildTask
    {
        [Required]
        public string BlobUrl { get; set; }

        [Output]
        public ITaskItem BlobElements { get; set; }

        public override bool Execute()
        {
            try
            {
                if (BlobUrl == null)
                {
                    Log.LogError($"No input blob url specified.");
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Parsing {0}", BlobUrl);

                    BlobUrlInfo info = new BlobUrlInfo(BlobUrl);

                    BlobElements = new TaskItem(BlobUrl);
                    BlobElements.SetMetadata("AccountName", info.AccountName);
                    BlobElements.SetMetadata("ContainerName", info.ContainerName);
                    BlobElements.SetMetadata("Endpoint", info.Endpoint);
                    BlobElements.SetMetadata("BlobPath", info.BlobPath);
                    return true;
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
