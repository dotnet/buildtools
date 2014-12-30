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
        /// The path to the packages.config to resolve packages from.
        /// </summary>
        [Required]
        public string PackagesConfig
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

            if (string.IsNullOrWhiteSpace(PackagesConfig))
            {
                Log.LogError("PackagesConfig property not specified.");
                return false;
            }

            if (!File.Exists(PackagesConfig))
            {
                Log.LogError("File '{0}' not found.", PackagesConfig);
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
                    foreach (var framework in Directory.EnumerateFiles(folder))
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
            var packagesConfig = new PackageReferenceFile(Path.GetFullPath(PackagesConfig));

            var succeeded = true;

            foreach (var packageReference in packagesConfig.GetPackageReferences())
            {
                var nupkgPath = TryLocatePackage(new DirectoryInfo(Path.GetDirectoryName(packagesConfig.FullPath)), packageReference);

                if (nupkgPath == null)
                {
                    Log.LogError("Unable to find nupkg for {0}.", packageReference.Id);
                    succeeded = false;
                    continue;
                }

                var package = new OptimizedZipPackage(nupkgPath);
                var manifest = ManifestDeclaration.Parse(Path.GetDirectoryName(nupkgPath), package);

                // If there are no assets we cannot 
                if (!manifest.Groups.Any())
                    continue;

                try
                {
                    Log.LogMessage("resolving assets from " + nupkgPath);

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
                                    if (Path.GetFileName(asset.Value) == "xunit.runner.tdnet.dll")
                                    {
                                        // skip this assembly, it isn't strong name signed and causes a compile error when it is
                                        // referenced from a signed assembly (such as those built in corefx) with the Mono mcs compiler.
                                        // csc.exe seems to ignore this as the reference isn't actually used anywhere.
                                        continue;
                                    }

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

        private static string TryLocatePackage(DirectoryInfo projectDirectory, PackageReference packageReference)
        {
            var parentDirectory = projectDirectory;
            var packageName = packageReference.Id + "." + packageReference.Version.ToString();

            do
            {
                var packagesDirectory = parentDirectory.GetDirectories("packages").FirstOrDefault();

                if (packagesDirectory != null)
                {
                    var nupkgPath = Path.Combine(packagesDirectory.FullName, packageName, packageName + ".nupkg");

                    if (File.Exists(nupkgPath))
                    {
                        return nupkgPath;
                    }
                }

                parentDirectory = parentDirectory.Parent;
            }
            while (parentDirectory != null);

            return null;
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
