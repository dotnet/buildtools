// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using MSBuild = Microsoft.Build.Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public sealed class ParseFeedUrl : MSBuild.Task
    {
        [Required]
        public string FeedUrl { get; set; }

        [Output]
        public ITaskItem BlobElements { get; set; }

        public override bool Execute()
        {
            try
            {
                if (FeedUrl == null)
                {
                    Log.LogError($"No input feed url specified.");
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Parsing {0}", FeedUrl);

                    BlobUrlInfo info = new BlobUrlInfo(FeedUrl);

                    // If the url doesn't end in "index.json", reject

                    if (!info.BlobPath.EndsWith("index.json"))
                    {
                        Log.LogError("Input feed url should end in index.json");
                    }

                    BlobElements = new TaskItem(FeedUrl);
                    BlobElements.SetMetadata("AccountName", info.AccountName);
                    BlobElements.SetMetadata("ContainerName", info.ContainerName);
                    BlobElements.SetMetadata("Endpoint", info.Endpoint);
                    BlobElements.SetMetadata("BaseBlobPath", info.BlobPath.Replace("/index.json", ""));
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
