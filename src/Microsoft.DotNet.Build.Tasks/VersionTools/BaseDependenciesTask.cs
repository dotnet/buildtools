// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Dependencies;
using Microsoft.DotNet.VersionTools.Dependencies.BuildOutput;
using Microsoft.DotNet.VersionTools.Dependencies.Submodule;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public abstract class BaseDependenciesTask : BuildTask
    {
        internal const string RawUrlMetadataName = "RawUrl";
        internal const string RawVersionsBaseUrlMetadataName = "RawVersionsBaseUrl";
        internal const string VersionsRepoDirMetadataName = "VersionsRepoDir";
        internal const string BuildInfoPathMetadataName = "BuildInfoPath";
        internal const string CurrentRefMetadataName = "CurrentRef";
        internal const string PackageIdMetadataName = "PackageId";
        internal const string VersionMetadataName = "Version";
        internal const string DependencyTypeMetadataName = "DependencyType";

        [Required]
        public ITaskItem[] DependencyInfo { get; set; }

        public ITaskItem[] ProjectJsonFiles { get; set; }

        public ITaskItem[] UpdateStep { get; set; }

        public string BuildInfoCacheDir { get; set; }

        public override bool Execute()
        {
            Trace.Listeners.MsBuildListenedInvoke(Log, TraceListenedExecute);
            return !Log.HasLoggedErrors;
        }

        protected abstract void TraceListenedExecute();

        protected Regex CreateXmlUpdateRegex(string elementName, string contentGroupName) =>
            new Regex($@"<{elementName}>(?<{contentGroupName}>.*)</{elementName}>");

        protected IEnumerable<IDependencyUpdater> CreateUpdaters()
        {
            if (ProjectJsonFiles != null && ProjectJsonFiles.Any())
            {
                yield return new ProjectJsonUpdater(ProjectJsonFiles.Select(item => item.ItemSpec));
            }

            foreach (ITaskItem step in UpdateStep ?? Enumerable.Empty<ITaskItem>())
            {
                string type = step.GetMetadata("UpdaterType");
                switch (type)
                {
                    case "Xml":
                        yield return CreateXmlUpdater(step);
                        break;

                    case "File":
                        yield return new FilePackageUpdater
                        {
                            PackageId = GetRequiredMetadata(step, "PackageId"),
                            Path = GetRequiredMetadata(step, "Path"),
                        };
                        break;

                    case "Tool versions":
                        yield return new ToolVersionsUpdater
                        {
                            Path = GetRequiredMetadata(step, "Path"),
                        };
                        break;

                    case "Submodule from package":
                        yield return new IndicatorPackageSubmoduleUpdater(
                            GetRequiredMetadata(step, "IndicatorPackage"))
                        {
                            PackageDownloadBaseUrl = GetRequiredMetadata(step, "PackageDownloadBaseUrl"),
                            Path = GetRequiredMetadata(step, "Path")
                        };
                        break;

                    case "Submodule from latest":
                        yield return new LatestCommitSubmoduleUpdater(
                            GetRequiredMetadata(step, "Repository"),
                            GetRequiredMetadata(step, "Ref"))
                        {
                            Path = GetRequiredMetadata(step, "Path")
                        };
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Unsupported updater '{step.ItemSpec}': UpdaterType '{type}'.");
                }
            }
        }

        protected IEnumerable<IDependencyInfo> CreateLocalDependencyInfos()
        {
            return CreateDependencyInfos(false, null);
        }

        protected IEnumerable<IDependencyInfo> CreateDependencyInfos(
            bool remote,
            string versionsCommit)
        {
            foreach (ITaskItem info in DependencyInfo ?? Enumerable.Empty<ITaskItem>())
            {
                string type = info.GetMetadata("DependencyType");
                switch (type)
                {
                    case "Build":
                        if (versionsCommit != null)
                        {
                            ReplaceExistingMetadata(info, CurrentRefMetadataName, versionsCommit);
                        }
                        yield return CreateBuildInfoDependency(info, BuildInfoCacheDir);
                        break;

                    case "Submodule":
                        yield return SubmoduleDependencyInfo.Create(
                            GetRequiredMetadata(info, "Repository"),
                            GetRequiredMetadata(info, "Ref"),
                            GetRequiredMetadata(info, "Path"),
                            remote);
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Unsupported DependencyInfo '{info.ItemSpec}': DependencyType '{type}'.");
                }
            }
        }

        private FileRegexUpdater CreateXmlUpdater(ITaskItem step)
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
            return updater;
        }

        private static TaskItemBuildDependencyInfo CreateBuildInfoDependency(ITaskItem item, string cacheDir)
        {
            BuildInfo info = CreateBuildInfo(item, cacheDir);

            bool updateStaticDependencies = item
                .GetMetadata("UpdateStableVersions")
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            string[] disabledPackages = item
                .GetMetadata("DisabledPackages")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            return new TaskItemBuildDependencyInfo(
                info,
                updateStaticDependencies,
                disabledPackages,
                item);
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

            // Optional: override base url with a local directory.
            string versionsRepoDir = item.GetMetadata(VersionsRepoDirMetadataName);

            if (!string.IsNullOrEmpty(versionsRepoDir) &&
                !string.IsNullOrEmpty(buildInfoPath))
            {
                return BuildInfo.LocalFileGetAsync(
                    item.ItemSpec,
                    versionsRepoDir,
                    buildInfoPath,
                    // Don't fetch latest release file: it may not be present in build from source.
                    fetchLatestReleaseFile: false).Result;
            }

            if (!string.IsNullOrEmpty(rawVersionsBaseUrl) &&
                !string.IsNullOrEmpty(buildInfoPath) &&
                !string.IsNullOrEmpty(currentRef))
            {
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

            throw new Exception($"Unable to create build info with '{item}'.");
        }

        private static string GetRequiredMetadata(ITaskItem item, string name)
        {
            string metadata = item.GetMetadata(name);
            if (string.IsNullOrEmpty(metadata))
            {
                throw new ArgumentException(
                    $"On '{item.ItemSpec}', did not find required '{name}' metadata.");
            }
            return metadata;
        }

        private static void ReplaceExistingMetadata(ITaskItem item, string name, string value)
        {
            if (!string.IsNullOrEmpty(item.GetMetadata(name)))
            {
                item.SetMetadata(name, value);
            }
        }
    }
}
