// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Dependencies;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public abstract class BaseDependenciesTask : Task
    {
        internal const string RawUrlMetadataName = "RawUrl";
        internal const string RawVersionsBaseUrlMetadataName = "RawVersionsBaseUrl";
        internal const string BuildInfoPathMetadataName = "BuildInfoPath";
        internal const string CurrentRefMetadataName = "CurrentRef";
        internal const string VersionsRepoAutoUpdateRefMetadataName = "VersionsRepoAutoUpdateRef";
        internal const string CurrentBranchMetadataName = "CurrentBranch";
        internal const string PackageIdMetadataName = "PackageId";
        internal const string VersionMetadataName = "Version";
        internal const string LatestReleaseVersionMetadataName = "LatestReleaseVersion";
        internal const string UpdateStableVersionsMetadataName = "UpdateStableVersions";

        internal const string AutoUpgradeBranchMetadataName = "Version";

        [Required]
        public ITaskItem[] DependencyBuildInfo { get; set; }

        public ITaskItem[] ProjectJsonFiles { get; set; }

        public ITaskItem[] XmlUpdateStep { get; set; }

        public bool AllowOnlySpecifiedPackages { get; set; }

        public string BuildInfoCacheDir { get; set; }

        public override bool Execute()
        {
            MsBuildTraceListener[] listeners = Trace.Listeners.AddMsBuildTraceListeners(Log);
            try
            {
                TraceListenedExecute();
            }
            finally
            {
                Trace.Listeners.RemoveMsBuildTraceListeners(listeners);
            }
            return !Log.HasLoggedErrors;
        }

        protected abstract void TraceListenedExecute();

        protected Regex CreateXmlUpdateRegex(string elementName, string contentGroupName) =>
            new Regex($@"<{elementName}>(?<{contentGroupName}>.*)</{elementName}>");

        protected IEnumerable<IDependencyUpdater> CreateUpdaters()
        {
            if (ProjectJsonFiles != null && ProjectJsonFiles.Any())
            {
                yield return new ProjectJsonUpdater(ProjectJsonFiles.Select(item => item.ItemSpec))
                {
                    AllowOnlySpecifiedPackages = AllowOnlySpecifiedPackages
                };
            }

            foreach (ITaskItem step in XmlUpdateStep ?? Enumerable.Empty<ITaskItem>())
            {
                string buildInfoName = step.GetMetadata("BuildInfoName");
                string packageId = step.GetMetadata("PackageId");

                FileRegexUpdater updater;

                if (!string.IsNullOrEmpty(buildInfoName))
                {
                    updater = new FileRegexReleaseUpdater
                    {
                        BuildInfoName = buildInfoName
                    };
                }
                else
                {
                    updater = new FileRegexPackageUpdater
                    {
                        PackageId = packageId
                    };
                }
                updater.Path = step.GetMetadata("Path");
                updater.Regex = CreateXmlUpdateRegex(step.GetMetadata("ElementName"), "version");
                updater.VersionGroupName = "version";

                yield return updater;
            }
        }

        protected IEnumerable<DependencyBuildInfo> CreateBuildInfoDependencies()
        {
            return DependencyBuildInfo
                ?.Select(item => CreateBuildInfoDependency(item, BuildInfoCacheDir))
                ?? Enumerable.Empty<DependencyBuildInfo>();
        }

        public static DependencyBuildInfo CreateBuildInfoDependency(ITaskItem item, string cacheDir)
        {
            BuildInfo info = CreateBuildInfo(item, cacheDir);

            bool updateStaticDependencies = item
                .GetMetadata(UpdateStableVersionsMetadataName)
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            string[] disabledPackages = item
                .GetMetadata("DisabledPackages")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            return new DependencyBuildInfo(info, updateStaticDependencies, disabledPackages);
        }

        private static BuildInfo CreateBuildInfo(ITaskItem item, string cacheDir)
        {
            string rawUrl = item.GetMetadata(RawUrlMetadataName);

            if (!string.IsNullOrEmpty(rawUrl))
            {
                return BuildInfo.Get(item.ItemSpec, rawUrl);
            }

            string rawVersionsBaseUrl = item.GetMetadata(RawVersionsBaseUrlMetadataName);
            string buildInfoPath = item.GetMetadata(BuildInfoPathMetadataName);
            string currentRef = item.GetMetadata(CurrentRefMetadataName);
            // Optional
            string currentBranch = item.GetMetadata(CurrentBranchMetadataName);

            if (!string.IsNullOrEmpty(rawVersionsBaseUrl) &&
                !string.IsNullOrEmpty(buildInfoPath) &&
                !string.IsNullOrEmpty(currentRef))
            {
                if (!string.IsNullOrEmpty(currentBranch))
                {
                    buildInfoPath = $"{buildInfoPath}/{currentBranch}";
                }

                return BuildInfo.CachedGet(
                    item.ItemSpec,
                    rawVersionsBaseUrl,
                    currentRef,
                    buildInfoPath,
                    cacheDir);
            }

            string packageId = item.GetMetadata(PackageIdMetadataName);
            string version = item.GetMetadata(VersionMetadataName);

            if (!string.IsNullOrEmpty(packageId) &&
                !string.IsNullOrEmpty(version))
            {
                return new BuildInfo
                {
                    Name = item.ItemSpec,
                    LatestPackages = new Dictionary<string, string>
                    {
                        [packageId] = version
                    }
                };
            }

            string latestReleaseVersion = item.GetMetadata(LatestReleaseVersionMetadataName);

            if (!string.IsNullOrEmpty(latestReleaseVersion))
            {
                return new BuildInfo
                {
                    Name = item.ItemSpec,
                    LatestPackages = new Dictionary<string, string>(),
                    LatestReleaseVersion = latestReleaseVersion
                };
            }

            throw new Exception($"Unable to create build info with '{item}'.");
        }
    }
}
