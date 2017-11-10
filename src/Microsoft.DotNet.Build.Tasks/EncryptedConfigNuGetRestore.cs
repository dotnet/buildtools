// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Use NuGet.Commands manually to perform a NuGet restore. This allows us to restore on the
    /// desktop framework on Windows, where encryption is available.
    /// 
    /// An alternative is to acquire nuget.exe, but it's heavier than NuGet.Commands and is harder
    /// to use in the build.
    /// </summary>
    public partial class EncryptedConfigNuGetRestore : BuildTask
    {
        public ITaskItem[] Inputs { get; set; }

        public string ConfigFile { get; set; }

        public string PackagesDir { get; set; }

        public override bool Execute()
        {
            var args = new RestoreArgs
            {
                Inputs = Inputs.Select(item => item.ItemSpec).ToList(),
                ConfigFile = ConfigFile,
                GlobalPackagesFolder = PackagesDir,

                Sources = new List<string>(),
                CacheContext = new SourceCacheContext(),
                RequestProviders = new List<IRestoreRequestProvider>
                {
                    new WorkaroundProjectJsonRestoreRequestProvider(new RestoreCommandProvidersCache())
                },
                Log = new NugetMsBuildLogger(new TaskLoggingHelper(this))
            };

            RestoreRunner.RunAsync(args).Wait();

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// A subclass of ProjectJsonRestoreRequestProvider, specialized to fix a bug.
        /// 
        /// NuGet issue: https://github.com/NuGet/Home/issues/6131
        /// 
        /// Implementation copied from:
        /// https://github.com/NuGet/NuGet.Client/blob/5d66754b948c86a39aa2b54e7e3c1d67f6b57979/src/NuGet.Core/NuGet.Commands/RestoreCommand/RequestFactory/ProjectJsonRestoreRequestProvider.cs
        /// </summary>
        private class WorkaroundProjectJsonRestoreRequestProvider : ProjectJsonRestoreRequestProvider, IRestoreRequestProvider
        {
            private RestoreCommandProvidersCache _providerCache;

            public WorkaroundProjectJsonRestoreRequestProvider(RestoreCommandProvidersCache providerCache)
                : base(providerCache)
            {
                _providerCache = providerCache;
            }

            Task<IReadOnlyList<RestoreSummaryRequest>> IRestoreRequestProvider.CreateRequests(
                string inputPath,
                RestoreArgs restoreContext)
            {
                var paths = new List<string>();

                if (Directory.Exists(inputPath))
                {
                    paths.AddRange(GetProjectJsonFilesInDirectory(inputPath));
                }
                else
                {
                    paths.Add(inputPath);
                }

                var requests = new List<RestoreSummaryRequest>(paths.Count);

                foreach (var path in paths)
                {
                    requests.Add(Create(path, restoreContext));
                }

                return System.Threading.Tasks.Task.FromResult<IReadOnlyList<RestoreSummaryRequest>>(requests);
            }

            private RestoreSummaryRequest Create(
                string inputPath,
                RestoreArgs restoreContext)
            {
                var file = new FileInfo(inputPath);

                // Get settings relative to the input file
                var settings = restoreContext.GetSettings(file.DirectoryName);
                
                // BUGFIX
                // The null here causes an exception downstream. Instead, inline the important code.
                //var sources = restoreContext.GetEffectiveSources(settings, null);
                var packageSourceProvider = new PackageSourceProvider(settings);
                CachingSourceProvider cachingSourceProvider = new CachingSourceProvider(packageSourceProvider);
                var sources = packageSourceProvider
                    .LoadPackageSources()
                    .Select(cachingSourceProvider.CreateRepository)
                    .ToList();
                // END BUGFIX
                var FallbackPackageFolders = restoreContext.GetEffectiveFallbackPackageFolders(settings);

                var globalPath = restoreContext.GetEffectiveGlobalPackagesFolder(file.DirectoryName, settings);

                var sharedCache = _providerCache.GetOrCreate(
                    globalPath,
                    FallbackPackageFolders,
                    sources,
                    restoreContext.CacheContext,
                    restoreContext.Log);

                var project = JsonPackageSpecReader.GetPackageSpec(file.Directory.Name, file.FullName);

                // BUGFIX
                // ApplyStandardProperties tries to access RestoreMetadata with no null check. Add
                // a default value.
                project.RestoreMetadata = new ProjectRestoreMetadata();
                // END BUGFIX

                var request = new RestoreRequest(
                    project,
                    sharedCache,
                    restoreContext.CacheContext,
                    restoreContext.Log);

                restoreContext.ApplyStandardProperties(request);

                IEnumerable<string> configFiles = SettingsUtility.GetConfigFilePaths(settings);

                var summaryRequest = new RestoreSummaryRequest(request, inputPath, configFiles, sources);

                return summaryRequest;
            }

            private static List<string> GetProjectJsonFilesInDirectory(string path)
            {
                try
                {
                    return Directory.GetFiles(
                            path,
                            $"*{ProjectJsonPathUtilities.ProjectConfigFileName}",
                            SearchOption.AllDirectories)
                        .Where(file => ProjectJsonPathUtilities.IsProjectConfig(file))
                        .ToList();
                }
                catch (UnauthorizedAccessException e)
                {
                    // Access to a subpath of the directory is denied.
                    var resourceMessage = "Access to a subpath of the directory is denied.";//Strings.Error_UnableToLocateRestoreTarget_Because;
                    var message = string.Format(CultureInfo.CurrentCulture, resourceMessage, path);

                    throw new InvalidOperationException(message, e);
                }
            }
        }
    }
}
