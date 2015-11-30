// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public class ApplyPreReleaseSuffix : ITask
    {
        private TaskLoggingHelper _log;

        public ApplyPreReleaseSuffix()
        {
            _log = new TaskLoggingHelper(this);
        }

        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        public ITaskHost HostObject
        {
            get;
            set;
        }

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

        public bool Execute()
        {
            if (null == StablePackages)
            {
                StablePackages = new ITaskItem[0];
            }

            if (null == OriginalPackages || OriginalPackages.Length == 0)
            {
                _log.LogError("OriginalPackages argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(PreReleaseSuffix))
            {
                _log.LogError("PreReleaseSuffix argument must be specified");
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
                TaskItem updatedPackage = new TaskItem(originalPackage);
                string packageId = originalPackage.ItemSpec;
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

            return !_log.HasLoggedErrors;
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
