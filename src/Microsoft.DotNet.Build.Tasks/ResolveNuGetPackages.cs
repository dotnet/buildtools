// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet;
using NuGet.NuManifest;
using NuGet.NuManifest.DotNet;
using NuGet.NuManifest.Resolution;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A task to resolve assets out of the packages referenced in a packages.config.
    /// </summary>
    public sealed class ResolveNuGetPackages : Task
    {
        /// <summary>
        /// Creates a new <see cref="ResolveNuGetPackages"/>.
        /// </summary>
        public ResolveNuGetPackages()
        {
            ResolvedReferences = new ITaskItem[0];
            ResolvedAnalyzers = new ITaskItem[0];
        }

        /// <summary>
        /// The path to the 'packages.config's to resolve packages from.
        /// </summary>
        [Required]
        public ITaskItem[] PackagesConfigs
        {
            get;
            set;
        }

        /// <summary>
        /// The path to the downloaded nuget package dependencies.
        /// </summary>
        [Required]
        public string PackageRoot
        {
            get;
            set;
        }

        /// <summary>
        /// The full paths to resolved build-time dependencies.
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedReferences
        {
            get;
            private set;
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
        /// The full paths to resolved copylocal.
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedCopyLocal
        {
            get;
            private set;
        }
        /// <summary>
        /// The name of the platform of binaries to choose. Examples include 'AnyCPU', 'x86', etc.
        /// </summary>
        public string Platform
        {
            get;
            private set;
        }

        /// <summary>
        /// Name of the configuration (Debug or Release) to choose.
        /// </summary>
        public string Configuration
        {
            get;
            private set;
        }

        /// <summary>
        /// The target framework moniker to use when selecting assets from packages.
        /// </summary>
        public string TargetFramework
        {
            get;
            private set;
        }

        /// <summary>
        /// The target platform moniker to use when selecting assets from packages.
        /// </summary>
        public string TargetPlatformMoniker
        {
            get;
            private set;
        }

        /// <summary>
        /// The language of the source files in the project.
        /// </summary>
        public string Language
        {
            get;
            private set;
        }

        /// <summary>
        /// Performs the NuGet package resolution.
        /// </summary>
        public override bool Execute()
        {
            var analyzers = new List<ITaskItem>();
            var references = new List<ITaskItem>();
            var copyLocals = new List<ITaskItem>();

            if (PackagesConfigs == null || PackagesConfigs.Length == 0)
            {
                Log.LogError("PackagesConfigs property not specified.");
                return false;
            }

            if (PackagesConfigs.Any(pc => string.IsNullOrWhiteSpace(pc.ItemSpec)))
            {
                Log.LogError("A PackagesConfigs element was not specified.");
            }

            ITaskItem packagesConfigNotFound = PackagesConfigs.FirstOrDefault(pc => !File.Exists(pc.ItemSpec));
            if (packagesConfigNotFound != default(ITaskItem))
            {
                Log.LogError("File '{0}' not found.", packagesConfigNotFound.ItemSpec);
                return false;
            }

            var targetFrameworks = new List<FrameworkName>();
            var targetFramework = new FrameworkName(TargetFramework);

            if (targetFramework.Identifier == ".NETPortable")
            {
                // Let's convert
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Reference Assemblies\Microsoft\Framework\.NETPortable", "v" + targetFramework.Version, "Profile", targetFramework.Profile, "SupportedFrameworks");

                if (Directory.Exists(folder))
                {
                    foreach (var framework in Directory.EnumerateFiles(folder, "*.xml"))
                    {
                        var xml = XDocument.Load(framework).Root;
                        targetFrameworks.Add(new FrameworkName(xml.Attribute("Identifier").Value, Version.Parse(xml.Attribute("MinimumVersion").Value)));
                    }
                }
                else
                {
                    // If the portable profile doesn't exist lets fallback to the default for Profile7. 
                    targetFrameworks.Add(new FrameworkName(".NETFramework", new Version(4, 5)));
                    targetFrameworks.Add(new FrameworkName(".NETCore", new Version(4, 5)));
                }
            }
            else
            {
                targetFrameworks.Add(targetFramework);
            }

            var context = AssetResolutionContext.Empty
                    .WithTargetFrameworks(new FrameworkNameSet(targetFrameworks))
                    .WithArchitecture(Platform ?? "")
                    .WithAssemblyAssetTypes()
                    .RegisterProperty("ProjectType", new LanguagePropertyDefinition())
                    .SetProperty("ProjectType", Language)
                    .RegisterProperty("DesignTimeOnly", new AnythingGoesPropertyDefinition())
                    .SetProperty("DesignTimeOnly", "true")
                    .RegisterAssetType("Analyzer");

            var platformNames = new List<PlatformName>();

            PlatformName singularPlatformName;
            if (PlatformName.TryParse(TargetPlatformMoniker, out singularPlatformName))
            {
                platformNames.Add(singularPlatformName);
            }

            if (platformNames.Any())
            {
                context = context.WithTargetPlatforms(new PlatformNameSet(platformNames));
            }

            var resolver = new AssetResolver(context);
            var packagesConfigs = (from packageConfig in PackagesConfigs select new PackageReferenceFile(Path.GetFullPath(packageConfig.ItemSpec))).ToArray();
            var succeeded = true;

            var repository = new SharedPackageRepository(PackageRoot);

            foreach (var packageReference in GetPackageReferences(packagesConfigs))
            {
                IPackage package;
                if (!repository.TryFindPackage(packageReference.Id, packageReference.Version, out package))
                {
                    Log.LogError("Unable to find nupkg for {0}.", packageReference.Id);
                    succeeded = false;
                    continue;
                }

                var packageDescription = packageReference.Id + "." + packageReference.Version.ToString();
                var manifest = ManifestDeclaration.Parse(Path.Combine(PackageRoot, packageDescription), package);

                // If there are no assets we cannot 
                if (!manifest.Groups.Any())
                {
                    Log.LogMessage("no assets for " + packageDescription);
                    continue;
                }

                try
                {
                    Log.LogMessage("resolving assets for " + packageDescription);

                    var assets = resolver.Resolve(manifest);

                    foreach (var asset in assets)
                    {
                        switch (asset.Type)
                        {
                            case "Analyzer":

                                analyzers.Add(new TaskItem(asset.Value));
                                Log.LogMessage("resolved analyzer: " + asset.Value);
                                break;

                            case WellKnownAssetType.ReferenceOnly:
                                {
                                    var taskItem = new TaskItem(asset.Value);

                                    taskItem.SetMetadata("NuGetPackageId", packageReference.Id);
                                    taskItem.SetMetadata("NuGetPackageVersion", packageReference.Version.ToString());
                                    taskItem.SetMetadata("ReferenceGrouping", packageReference.Id + ",Version=" + packageReference.Version.Version);
                                    taskItem.SetMetadata("ReferenceGroupingDisplayName", packageReference.Id);
                                    taskItem.SetMetadata("Private", "false");
                                    references.Add(taskItem);

                                    Log.LogMessage("resolved reference: " + asset.Value);
                                    break;
                                }
                            case WellKnownAssetType.CopyLocal:
                                {
                                    var taskItem = new TaskItem(asset.Value);
                                    taskItem.SetMetadata("CopyToOutputDirectory", "PreserveNewest");
                                    copyLocals.Add(taskItem);

                                    Log.LogMessage("resolved copylocal: " + asset.Value);
                                    break;
                                }
                        }
                    }
                }
                catch (AssetResolutionException ex)
                {
                    Log.LogWarning("Unable to resolve the assets of {0}: {1}", packageReference.Id, ex.Message);
                }
            }

            ResolvedAnalyzers = analyzers.ToArray();
            ResolvedReferences = references.ToArray();
            ResolvedCopyLocal = copyLocals.ToArray();

            return succeeded;
        }

        private IEnumerable<PackageReference> GetPackageReferences(PackageReferenceFile[] packagesConfigs)
        {
            // Return the highest version of each package found in every packages.config

            return
                from packagesConfig in packagesConfigs
                from packageReferences in packagesConfig.GetPackageReferences()
                group packageReferences by packageReferences.Id into packageReferencesGroup
                select packageReferencesGroup.OrderByDescending(pr => pr.Version).First();
        }

        private sealed class LanguagePropertyDefinition : PropertyDefinition<string>
        {
            public override bool IsApplicable(string configuredValue, string value)
            {
                return configuredValue == value;
            }

            protected override int Compare(string configuredValue, string value1, string value2)
            {
                // There's no relative comparison that can be done here.
                return 0;
            }

            protected override bool TryParse(string valueText, out string value)
            {
                value = valueText;
                return true;
            }
        }

        private class AnythingGoesPropertyDefinition : PropertyDefinition<string>
        {
            public override bool IsApplicable(string configuredValue, string value)
            {
                return true;
            }

            protected override int Compare(string configuredValue, string value1, string value2)
            {
                // Just prefer the first one.
                return -1;
            }

            protected override bool TryParse(string valueText, out string value)
            {
                value = valueText;
                return true;
            }
        }
    }
}
