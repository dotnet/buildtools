// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// This task will determine if a set of packages need to be stable based on another set.
    /// If not stable, it will append a pre-release suffix.  It will also standardize on 3-part versions.
    /// </summary>
    public class ApplyPreReleaseSuffix : PackagingTask
    {
        public ITaskItem[] StablePackages
        {
            get;
            set;
        }

        [Required]
        public ITaskItem[] OriginalPackages
        {
            get;
            set;
        }

        public bool RevStableToPrerelease
        {
            get;
            set;
        }

        [Required]
        public string PreReleaseSuffix
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] UpdatedPackages
        {
            get;
            set;
        }

        public override bool Execute()
        {
            if (null == StablePackages)
            {
                StablePackages = new ITaskItem[0];
            }

            if (null == OriginalPackages || OriginalPackages.Length == 0)
            {
                Log.LogError("OriginalPackages argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(PreReleaseSuffix))
            {
                Log.LogError("PreReleaseSuffix argument must be specified");
                return false;
            }

            // build up a map of stable versions
            Dictionary<string, Version> stablePackageVersions = new Dictionary<string, Version>();
            foreach (var stablePackage in StablePackages)
            {
                string stablePackageId = stablePackage.ItemSpec;
                Version newVersion = ParseAs3PartVersion(stablePackage.GetMetadata("Version"));
                Version existingVersion = null;

                // if we don't have a version or the new version is greater assign it
                if (!stablePackageVersions.TryGetValue(stablePackageId, out existingVersion) ||
                    (newVersion > existingVersion))
                {
                    stablePackageVersions[stablePackageId] = newVersion;
                }
            }

            List<ITaskItem> updatedPackages = new List<ITaskItem>();

            foreach (var originalPackage in OriginalPackages)
            {
                string packageId = originalPackage.ItemSpec;

                if (packageId == "_._")
                {
                    updatedPackages.Add(originalPackage);
                    continue;
                }

                TaskItem updatedPackage = new TaskItem(originalPackage);
                Version packageVersion = ParseAs3PartVersion(originalPackage.GetMetadata("Version"));
                Version stableVersion = null;

                // if the id is not in the stable versions, or the stable version is lower append pre-release
                if (!stablePackageVersions.TryGetValue(packageId, out stableVersion) ||
                    stableVersion < packageVersion)
                {
                    // pre-release, set with suffix
                    updatedPackage.SetMetadata("Version", packageVersion.ToString() + PreReleaseSuffix);
                }
                else if (RevStableToPrerelease)
                {
                    // stable contract, but we want to rev the package version as a workaround
                    // until we are able to actually rev the assembly version
                    packageVersion = new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build + 1);
                    // pre-release, set with suffix
                    updatedPackage.SetMetadata("Version", packageVersion.ToString() + PreReleaseSuffix);
                }
                else
                {
                    // stable, just set the 3 part version witout suffix
                    updatedPackage.SetMetadata("Version", packageVersion.ToString());
                }

                updatedPackages.Add(updatedPackage);
            }

            UpdatedPackages = updatedPackages.ToArray();

            return !Log.HasLoggedErrors;
        }

        private static Version ParseAs3PartVersion(string versionString)
        {
            Version result = new Version(versionString);
            if (result.Revision != -1)
            {
                result = new Version(result.Major, result.Minor, result.Build);
            }
            return result;
        }
    }
}
