// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// This task will determine if a set of packages need to be stable based on another set.
    /// If not stable, it will append a pre-release suffix.  It will also standardize on 3-part versions.
    /// </summary>
    public class ApplyPreReleaseSuffix : PackagingTask
    {
        /// <summary>
        /// Original dependencies without pre-release specifier.
        /// </summary>
        [Required]
        public ITaskItem[] OriginalPackages { get; set; }

        /// <summary>
        /// Pre-release suffix for this build.
        /// </summary>
        [Required]
        public string PreReleaseSuffix { get; set; }

        /// <summary>
        /// Package index files used to define stable package list.
        /// </summary>
        [Required]
        public ITaskItem[] PackageIndexes { get; set; }

        /// <summary>
        /// Updated dependencies whit pre-release specifier where package version is not yet stable.
        /// </summary>
        [Output]
        public ITaskItem[] UpdatedPackages { get; set; }

        public override bool Execute()
        {
            if (null == OriginalPackages || OriginalPackages.Length == 0)
            {
                Log.LogError($"{nameof(OriginalPackages)} argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(PreReleaseSuffix))
            {
                Log.LogError($"{nameof(PreReleaseSuffix)} argument must be specified");
                return false;
            }

            if (PackageIndexes == null || PackageIndexes.Length == 0)
            {
                Log.LogError($"{nameof(PackageIndexes)} must be specified");
                return false;
            }

            PackageIndex.Current.Merge(PackageIndexes.Select(pi => pi.GetMetadata("FullPath")));

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

                if (!PackageIndex.Current.IsStable(packageId, packageVersion))
                {
                    // pre-release, set with suffix
                    updatedPackage.SetMetadata("Version", packageVersion.ToString() + PreReleaseSuffix);
                }
                else
                {
                    // stable, just set the 3 part version without suffix
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
