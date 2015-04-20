// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Repositories;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NuGet.Build.Tasks
{
    /// <summary>
    /// Resolves the assets out of packages in the project.json.
    /// </summary>
    public sealed class ResolveNuGetPackageAssets : Task
    {
        /// <summary>
        /// Creates a new <see cref="ResolveNuGetPackageAssets"/>.
        /// </summary>
        public ResolveNuGetPackageAssets()
        {
            ResolvedAnalyzers = new ITaskItem[0];
            ResolvedCopyLocalItems = new ITaskItem[0];
            ResolvedReferences = new ITaskItem[0];
        }

        /// <summary>
        /// The full paths to resolved analyzers.
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedAnalyzers
        {
            get;
            private set;
        }

        /// <summary>
        /// The full paths to resolved run-time resources.
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedCopyLocalItems
        {
            get;
            private set;
        }

        /// <summary>
        /// The full paths to resolved build-time dependencies. Contains standard metadata for Reference items.
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedReferences
        {
            get;
            private set;
        }

        /// <summary>
        /// The name of the architecture of binaries to choose. Examples include 'AnyCPU', 'x86', etc.
        /// </summary>
        public string Architecture
        {
            get; set;
        }

        /// <summary>
        /// The name (Debug or Release) of the configuration to choose.
        /// </summary>
        public string Configuration
        {
            get; set;
        }

        /// <summary>
        /// The target framework monikers to use when selecting assets from packages.
        /// </summary>
        [Required]
        public string[] TargetFrameworkMonikers
        {
            get; set;
        }

        /// <summary>
        /// The target platform monikers to use when selecting assets from packages.
        /// </summary>
        [Required]
        public string[] TargetPlatformMonikers
        {
            get; set;
        }

        [Required]
        public ITaskItem[] NuGetPackageReferences
        {
            get; set;
        }

        [Required]
        public string ProjectFile
        {
            get; set;
        }

        /// <summary>
        /// The path to the downloaded nuget package dependencies.
        /// </summary>
        public string PackageRoot
        {
            get;
            set;
        }

        public bool UseDotNetNativeToolchain
        {
            get; set;
        }

        public string IlcTargetFrameworkPath
        {
            get; set;
        }

        public string IlcTargetFrameworkFacadesPath
        {
            get; set;
        }

        /// <summary>
        /// The language of the source files in the project.
        /// </summary>
        public string Language
        {
            get; private set;
        }

        /// <summary>
        /// Performs the NuGet package resolution.
        /// </summary>
        public override bool Execute()
        {
            var analyzers = new List<ITaskItem>();
            var copyLocalItems = new List<ITaskItem>();
            var references = new List<ITaskItem>();

            var providers = new List<IDependencyProvider>();

            if (!String.IsNullOrEmpty(PackageRoot))
            {
                providers.Add(new NuGetDependencyResolver(PackageRoot));
            }
            else
            {
                // Find and add all our package folders
                var parentDirectory = new DirectoryInfo(Environment.CurrentDirectory);

                while (parentDirectory != null)
                {
                    if (parentDirectory.GetDirectories("packages").Any())
                    {
                        providers.Add(new NuGetDependencyResolver(Path.Combine(parentDirectory.FullName, "packages")));
                    }

                    parentDirectory = parentDirectory.Parent;
                }

                providers.Add(new NuGetDependencyResolver(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dnx", "packages")));
            }

            providers.Add(new MSBuildDependencyProvider(ProjectFile, NuGetPackageReferences));

            var walker = new DependencyWalker(providers);
            var root = walker.Walk(ProjectFile, new NuGetVersion(new Version()), NuGetFramework.Parse(TargetFrameworkMonikers.First()));
            root.TryResolveConflicts();

            var resolvedItems = new Dictionary<string, Library>();

            // Pick the relevant versions of the package after conflict
            // resolution
            root.ForEach(true, (node, state) =>
            {
                if (node.Disposition != Disposition.Accepted ||
                    node.Item == null)
                {
                    return false;
                }

                if (!resolvedItems.ContainsKey(node.Key.Name))
                {
                    resolvedItems[node.Key.Name] = node.Item.Data;
                }

                return true;
            });

            var patternDefinitions = new PatternDefinitions();

            if (UseDotNetNativeToolchain)
            {
                var directoriesToPrep = new[] { IlcTargetFrameworkPath, IlcTargetFrameworkFacadesPath };
                foreach (var directoryToPrep in directoriesToPrep)
                {
                    var directoryInfoToPrep = new DirectoryInfo(directoryToPrep);
                    directoryInfoToPrep.Create();
                    foreach (var file in directoryInfoToPrep.GetFiles())
                    {
                        file.Delete();
                    }
                }
            }

            foreach (var library in resolvedItems.Values)
            {
                if (library.Identity.Type != LibraryTypes.Package)
                {
                    continue;
                }

                Log.LogMessage(MessageImportance.Low, "Resolved references from {0}:", library.Identity.ToString());

                LoadContents(library);

                var compileTimeReferences = TryGetTaskItemsFromLibrary(library, patternDefinitions.CompileTimeAssemblies);
                var copyLocalReferences = TryGetTaskItemsFromLibrary(library, patternDefinitions.ManagedAssemblies) ?? Enumerable.Empty<ITaskItem>(); 
                var copyLocalNativeReferences = TryGetTaskItemsFromLibrary(library, patternDefinitions.NativeLibraries) ?? Enumerable.Empty<ITaskItem>();
                var itemsInAot = TryGetTaskItemsFromLibrary(library, patternDefinitions.AheadOfTimeAssemblies) ?? Enumerable.Empty<ITaskItem>();

                if (compileTimeReferences == null)
                {
                    // Then the "copy local" references are actually our compile-time references
                    compileTimeReferences = copyLocalReferences;
                }

                if (compileTimeReferences != null)
                {
                    ApplyCompileTimeReferenceMetadata(compileTimeReferences, library);

                    foreach (var compileTimeReference in compileTimeReferences)
                    {
                        Log.LogMessage(MessageImportance.Low, "    Build time reference: {0}", compileTimeReference.ItemSpec);
                        references.Add(compileTimeReference);
                    }
                }

                if (UseDotNetNativeToolchain)
                {
                    var frameworkAotItems = itemsInAot.Any() ? itemsInAot : copyLocalReferences;
                    foreach (var frameworkAotItem in frameworkAotItems)
                    {
                        File.Copy(frameworkAotItem.ItemSpec, Path.Combine(IlcTargetFrameworkFacadesPath, Path.GetFileName(frameworkAotItem.ItemSpec)), overwrite: true);
                    }

                    foreach (var referenceItem in compileTimeReferences)
                    {
                        File.Copy(referenceItem.ItemSpec, Path.Combine(IlcTargetFrameworkPath, Path.GetFileName(referenceItem.ItemSpec)), overwrite: true);
                    }
                }
                else
                {
                    foreach (var copyLocalReference in copyLocalReferences.Concat(copyLocalNativeReferences))
                    {
                        Log.LogMessage(MessageImportance.Low, "    Copy local reference: {0}", copyLocalReference.ItemSpec);
                        copyLocalItems.Add(copyLocalReference);
                    }
                }
            }

            ResolvedAnalyzers = analyzers.ToArray();
            ResolvedCopyLocalItems = copyLocalItems.ToArray();
            ResolvedReferences = references.ToArray();

            return true;
        }

        private IEnumerable<ITaskItem> TryGetTaskItemsFromLibrary(Library library, ContentPatternDefinition definition)
        {
            var taskItems = new List<ITaskItem>();
            var contents = library.GetItem<ContentItemCollection>("contents");

            if (contents == null)
            {
                return taskItems;
            }

            var group = contents.FindBestItemGroup(GetSelectionCriteria(), definition);

            if (group == null)
            {
                return null;
            }

            foreach (var item in group.Items.Where(i => Path.GetFileName(i.Path) != "_._"))
            {
                var taskItem = new TaskItem(Path.Combine(Path.GetDirectoryName(library.Path), item.Path.Replace('/', '\\')));

                taskItem.SetMetadata("NuGetPackageName", library.Identity.Name);
                taskItem.SetMetadata("NuGetPackageVersion", library.Identity.Version.ToString());

                taskItems.Add(taskItem);
            }

            return taskItems;
        }

        private void ApplyCompileTimeReferenceMetadata(IEnumerable<ITaskItem> items, Library library)
        {
            foreach (var item in items)
            {
                item.SetMetadata("ReferenceGrouping", library.Identity.Name + ",Version=" + library.Identity.Version.Version);
                item.SetMetadata("ReferenceGroupingDisplayName", library.Identity.Name + " (Package)");
                item.SetMetadata("Private", "false");
            }
        }

        private void LoadContents(Library library)
        {
            var contents = new ContentItemCollection();
            var files = library.GetItem<IEnumerable<string>>("files");

            if (files != null)
            {
                contents.Load(files);
            }
            else
            {
                var packageInfo = library.GetItem<LocalPackageInfo>("package");

                if (packageInfo == null)
                {
                    return;
                }

                contents.Load(packageInfo.ExpandedPath);
            }

            library.Items["contents"] = contents;
        }

        private SelectionCriteria GetSelectionCriteria()
        {
            var criteria = new SelectionCriteria();

            criteria.Entries.Add(new SelectionCriteriaEntry
            {
                Properties = new Dictionary<string, object>
                {
                    { "tpm", NuGetFramework.Parse(TargetPlatformMonikers.First()) },
                    { "tfm", NuGetFramework.Parse(TargetFrameworkMonikers.First()) },
                    { "arch", Architecture }
                }
            });

            criteria.Entries.Add(new SelectionCriteriaEntry
            {
                Properties = new Dictionary<string, object>
                {
                    { "tfm", NuGetFramework.Parse(TargetFrameworkMonikers.First()) }
                }
            });

            return criteria;
        }
    }
}
