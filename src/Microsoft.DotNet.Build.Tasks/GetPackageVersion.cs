// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetPackageVersion : Task
    {
        [Required]
        public string RepositoryRoot { get; set; }

        [Required]
        public string PackageName { get; set; }

        [Required]
        public bool RequireExplicitVersions { get; set; }

        [Output]
        public string PackageVersion { get; set; }

        public override bool Execute()
        {
            Log.LogMessage("Starting GetPackageVersion");

            using (var repository = new Repository(RepositoryRoot))
            {
                string tipSha = repository.Head.Tip.Sha;
                string prefix = PackageName + "-";

                // Get tags that begin with the package name, on the tip
                string[] matchingTags = repository.Tags.Where(t =>
                        t.Target.Sha == tipSha &&
                        t.Name.StartsWith(prefix)).Select(t => t.Name).ToArray();

                // Verify we didn't have multiple matching tags
                Action<string> throwTagException = (message) => { throw new InvalidOperationException(string.Format(message, tipSha, prefix)); };
                if (matchingTags.Count() > 1)
                    throwTagException("Multiple tags found on commit {0} beginning with {1} (expected one)");

                if (matchingTags.Count() == 1)
                {
                    PackageVersion = matchingTags[0].Substring(prefix.Length);
                }
                else
                {
                    if (RequireExplicitVersions == true)
                        throwTagException("No tag found on commit {0} beginning with {1} (expected one)");

                    PackageVersion = GetImplicitVersion(repository.Head.Tip.Sha);
                }
            }

            Log.LogMessage(string.Format("GetPackageVersion completed successfully - chose version {0}", PackageVersion));

            return true;
        }

        private string GetImplicitVersion(string sha)
        {
            return string.Format("999.999.999-sha{0}", sha.Substring(0, 10));
        }
    }
}