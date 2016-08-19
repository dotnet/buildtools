using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GetLastStablePackage : PackagingTask
    {
        /// <summary>
        /// List of packages to look up.
        /// The closest StablePackage version that is less than the version of each of these packages will be returned in LastStablePackages.
        ///   Identity: Package ID
        ///   Version: Package version.
        /// </summary>
        [Required]
        public ITaskItem[] LatestPackages { get; set; }
        
        /// <summary>
        /// List of previously shipped packages.
        ///   Identity: Package ID
        ///   Version: Package version.
        /// </summary>
        [Required]
        public ITaskItem[] StablePackages { get; set; }

        /// <summary>
        /// Latest version from StablePackages for all packages in LatestPackages.
        /// If a version isn't found for an item in LatestPackage that will not be included in this set.
        /// </summary>
        [Output]
        public ITaskItem[] LastStablePackages { get; set; }

        public override bool Execute()
        {
            if (LatestPackages == null || LatestPackages.Length == 0)
            {
                return true;
            }

            Dictionary<string, Version> latestPackages = new Dictionary<string, Version>();
            Dictionary<string, Version> lastStablePackages = new Dictionary<string, Version>();
            
            foreach(var latestPackage in LatestPackages)
            {
                var packageId = latestPackage.ItemSpec;

                var versionString = latestPackage.GetMetadata("Version");
                NuGetVersion nuGetVersion = null;
                if (versionString == null || !NuGetVersion.TryParse(versionString, out nuGetVersion))
                {
                    Log.LogMessage($"Could not parse version {versionString} for LatestPackage {packageId}, will use latest stable.");
                }

                latestPackages[packageId] = nuGetVersion?.Version;
            }

            foreach(var stablePackage in StablePackages)
            {
                var packageId = stablePackage.ItemSpec;

                Version latestVersion;
                if (!latestPackages.TryGetValue(packageId, out latestVersion))
                {
                    continue;
                }

                var versionString = stablePackage.GetMetadata("Version");
                Version stableVersion;
                if (versionString == null || !Version.TryParse(versionString, out stableVersion))
                {
                    Log.LogError($"Could not parse version {versionString} for StablePackage {packageId}");
                    continue;
                }

                // need a version less than current version
                if (latestVersion != null && stableVersion >= latestVersion)
                {
                    continue;
                }

                Version lastStableVersion;
                if (!lastStablePackages.TryGetValue(packageId, out lastStableVersion) || lastStableVersion < stableVersion)
                {
                    lastStablePackages[packageId] = stableVersion;
                }
            }

            LastStablePackages = lastStablePackages.Select(p => CreateItem(p.Key, p.Value)).ToArray();

            return !Log.HasLoggedErrors;
        }

        private ITaskItem CreateItem(string id, Version version)
        {
            var item = new TaskItem(id);
            item.SetMetadata("Version", version.ToString(3));
            return item;
        }
    }
}
