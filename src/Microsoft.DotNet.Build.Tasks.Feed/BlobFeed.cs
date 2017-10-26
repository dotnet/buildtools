// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public sealed class BlobFeed
    {
        private MSBuild.TaskLoggingHelper Log;

        public string AccountName { get; set; }

        public string AccountKey { get; set; }

        public string ContainerName { get; set; }

        public string RelativePath { get; set; }


        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;

        public BlobFeed(string accountName, string accountKey, string containerName, string relativePath, MSBuild.TaskLoggingHelper loggingHelper)
        {
            AccountName = accountName;
            AccountKey = accountKey;
            ContainerName = containerName;
            Log = loggingHelper;
            RelativePath = relativePath;
        }

        public bool IsSanityChecked(IEnumerable<string> items)
        {
            Log.LogMessage(MessageImportance.Low, $"START checking sanitized items for feed");
            foreach (var item in items)
            {
                if (items.Any(s => Path.GetExtension(item) != ".nupkg"))
                {
                    Log.LogError($"{item} is not a nupkg");
                    return false;
                }
            }
            List<string> duplicates = items.GroupBy(x => x)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key).ToList();
            if (duplicates.Count > 0)
            {
                Log.LogError($"Duplicates found: {duplicates}");
                return false;
            }
            Log.LogMessage(MessageImportance.Low, $"DONE checking for sanitized items for feed");
            return true;
       }
    }
}
