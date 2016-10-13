// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.ContentModel;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class HarvestPackage : PackagingTask
    {
        /// <summary>
        /// Package ID to harvest
        /// </summary>
        [Required]
        public string PackageId { get; set; }

        /// <summary>
        /// Current package version.
        /// </summary>
        [Required]
        public string PackageVersion { get; set; }

        /// <summary>
        /// Folder where packages have been restored
        /// </summary>
        [Required]
        public string PackagesFolder { get; set; }

        /// <summary>
        /// Path to runtime.json that contains the runtime graph.
        /// </summary>
        [Required]
        public string RuntimeFile { get; set; }

        /// <summary>
        /// Additional packages to consider for evaluating support but not harvesting assets.
        ///   Identity: Package ID
        ///   Version: Package version.
        /// </summary>
        public ITaskItem[] RuntimePackages { get; set; }

        /// <summary>
        /// Set to false to suppress harvesting of files and only harvest supported framework information.
        /// </summary>
        public bool HarvestAssets { get; set; }

        /// <summary>
        /// Set to partial paths to exclude from file harvesting.
        /// </summary>
        public string[] PathsToExclude { get; set; }

        /// <summary>
        /// Set to partial paths to suppress from both file and support harvesting.
        /// </summary>
        public string[] PathsToSuppress { get; set; }

        /// <summary>
        /// Frameworks to consider for support evaluation.
        ///   Identity: Framework
        ///   RuntimeIDs: Semi-colon seperated list of runtime IDs
        /// </summary>
        public ITaskItem[] Frameworks { get; set; }

        /// <summary>
        /// Files already in the package.
        ///   Identity: path to file
        ///   AssemblyVersion: version of assembly
        ///   TargetFramework: target framework moniker to use for harvesting file's dependencies
        ///   TargetPath: path of file in package
        ///   IsReferenceAsset: true for files in Ref.
        /// </summary>
        public ITaskItem[] Files { get; set; }

        /// <summary>
        /// Frameworks that were supported by previous package version.
        ///   Identity: Framework
        ///   Version: Assembly version if supported
        /// </summary>
        [Output]
        public ITaskItem[] SupportedFrameworks { get; set; }

        /// <summary>
        /// Files harvested from previous package version.
        ///   Identity: path to file
        ///   AssemblyVersion: version of assembly
        ///   TargetFramework: target framework moniker to use for harvesting file's dependencies
        ///   TargetPath: path of file in package
        ///   IsReferenceAsset: true for files in Ref.
        /// </summary>
        [Output]
        public ITaskItem[] HarvestedFiles { get; set; }

        /// <summary>
        /// When Files are specified, contains the updated set of files, with removals.
        /// </summary>
        [Output]
        public ITaskItem[] UpdatedFiles { get; set; }

        /// <summary>
        /// Generates a table in markdown that lists the API version supported by 
        /// various packages at all levels of NETStandard.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            if (!Directory.Exists(PackagesFolder))
            {
                Log.LogError($"PackagesFolder {PackagesFolder} does not exist.");
            }

            if (HasPackagesToHarvest())
            {
                if (HarvestAssets)
                {
                    HarvestFilesFromPackage();
                }

                if (Frameworks != null && Frameworks.Length > 0)
                {
                    HarvestSupportedFrameworks();
                }
            }

            return !Log.HasLoggedErrors;
        }

        private bool HasPackagesToHarvest()
        {
            bool result = true;

            IEnumerable<string> packageDirs = new[] { Path.Combine(PackageId, PackageVersion) };

            if (RuntimePackages != null)
            {
                packageDirs = packageDirs.Concat(
                    RuntimePackages.Select(p => Path.Combine(p.ItemSpec, p.GetMetadata("Version"))));
            }

            foreach (var packageDir in packageDirs)
            {
                var pathToPackage = Path.Combine(PackagesFolder, packageDir);
                if (!Directory.Exists(pathToPackage))
                {
                    Log.LogMessage(LogImportance.Low, $"Will not harvest files & support from package {packageDir} because {pathToPackage} does not exist.");
                    result = false;
                }
            }

            return result;
        }

        private void HarvestSupportedFrameworks()
        {
            List<ITaskItem> supportedFrameworks = new List<ITaskItem>();

            AggregateNuGetAssetResolver resolver = new AggregateNuGetAssetResolver(RuntimeFile);
            string packagePath = Path.Combine(PackagesFolder, PackageId, PackageVersion);

            // add the primary package
            resolver.AddPackageItems(PackageId, GetPackageItems(packagePath));

            if (RuntimePackages != null)
            {
                // add any split runtime packages
                foreach (var runtimePackage in RuntimePackages)
                {
                    var runtimePackageId = runtimePackage.ItemSpec;
                    var runtimePackageVersion = runtimePackage.GetMetadata("Version");

                    resolver.AddPackageItems(runtimePackageId, GetPackageItems(PackagesFolder, runtimePackageId, runtimePackageVersion));
                }
            }

            // create a resolver that can be used to determine the API version for inbox assemblies
            // since inbox assemblies are represented with placeholders we can remove the placeholders
            // and use the netstandard reference assembly to determine the API version
            var filesWithoutPlaceholders = GetPackageItems(packagePath)
                .Where(f => !NuGetAssetResolver.IsPlaceholder(f));
            NuGetAssetResolver resolverWithoutPlaceholders = new NuGetAssetResolver(RuntimeFile, filesWithoutPlaceholders);

            string package = $"{PackageId}/{PackageVersion}";

            foreach (var framework in Frameworks)
            {
                var runtimeIds = framework.GetMetadata("RuntimeIDs")?.Split(';');

                NuGetFramework fx;
                try
                {
                    fx = FrameworkUtilities.ParseNormalized(framework.ItemSpec);
                }
                catch (Exception ex)
                {
                    Log.LogError($"Could not parse Framework {framework.ItemSpec}. {ex}");
                    continue;
                }

                if (fx.Equals(NuGetFramework.UnsupportedFramework))
                {
                    Log.LogError($"Did not recognize {framework.ItemSpec} as valid Framework.");
                    continue;
                }

                var compileAssets = resolver.ResolveCompileAssets(fx, PackageId);

                bool hasCompileAsset, hasCompilePlaceHolder;
                NuGetAssetResolver.ExamineAssets(Log, "Compile", package, fx.ToString(), compileAssets, out hasCompileAsset, out hasCompilePlaceHolder);

                // start by making sure it has some asset available for compile
                var isSupported = hasCompileAsset || hasCompilePlaceHolder;

                if (!isSupported)
                {
                    Log.LogMessage(LogImportance.Low, $"Skipping {fx} because it is not supported.");
                    continue;
                }

                foreach (var runtimeId in runtimeIds)
                {
                    string target = String.IsNullOrEmpty(runtimeId) ? fx.ToString() : $"{fx}/{runtimeId}";

                    var runtimeAssets = resolver.ResolveRuntimeAssets(fx, runtimeId);

                    bool hasRuntimeAsset, hasRuntimePlaceHolder;
                    NuGetAssetResolver.ExamineAssets(Log, "Runtime", package, target, runtimeAssets, out hasRuntimeAsset, out hasRuntimePlaceHolder);

                    isSupported &= hasCompileAsset == hasRuntimeAsset;
                    isSupported &= hasCompilePlaceHolder == hasRuntimePlaceHolder;

                    if (!isSupported)
                    {
                        Log.LogMessage(LogImportance.Low, $"Skipping {fx} because it is not supported on {target}.");
                        break;
                    }
                }

                if (isSupported)
                {
                    var supportedFramework = new TaskItem(framework.ItemSpec);
                    supportedFramework.SetMetadata("HarvestedFromPackage", package);

                    // set version

                    // first try the resolved compile asset for this package
                    var refAssm = compileAssets.FirstOrDefault(r => !NuGetAssetResolver.IsPlaceholder(r))?.Substring(PackageId.Length + 1);

                    if (refAssm == null)
                    {
                        // if we didn't have a compile asset it means this framework is supported inbox with a placeholder
                        // resolve the assets without placeholders to pick up the netstandard reference assembly.
                        compileAssets = resolverWithoutPlaceholders.ResolveCompileAssets(fx);
                        refAssm = compileAssets.FirstOrDefault(r => !NuGetAssetResolver.IsPlaceholder(r));
                    }

                    string version = "unknown";
                    if (refAssm != null)
                    {
                        version = VersionUtility.GetAssemblyVersion(Path.Combine(packagePath, refAssm))?.ToString() ?? version;
                    }

                    supportedFramework.SetMetadata("Version", version);

                    Log.LogMessage(LogImportance.Low, $"Validating version {version} for {supportedFramework.ItemSpec} because it was supported by {PackageId}/{PackageVersion}.");

                    supportedFrameworks.Add(supportedFramework);
                }
            }

            SupportedFrameworks = supportedFrameworks.ToArray();
        }

        public void HarvestFilesFromPackage()
        {
            string pathToPackage = Path.Combine(PackagesFolder, PackageId, PackageVersion);

            if (!Directory.Exists(pathToPackage))
            {
                Log.LogError($"Cannot harvest from package {PackageId}/{PackageVersion} because {pathToPackage} does not exist.");
                return;
            }

            var livePackageFiles = Files.NullAsEmpty()
                .Where(f => IsIncludedExtension(f.GetMetadata("Extension")))
                .Select(f => new PackageItem(f))
                .ToDictionary(p => p.TargetPath, StringComparer.OrdinalIgnoreCase);

            var harvestedFiles = new List<ITaskItem>();
            var removeFiles = new List<ITaskItem>();

            // make sure we preserve refs that match desktop assemblies
            var liveDesktopDlls = livePackageFiles.Values.Where(pi => pi.IsDll && pi.TargetFramework?.Framework == FrameworkConstants.FrameworkIdentifiers.Net);
            var desktopRefVersions = liveDesktopDlls.Where(d => d.IsRef && d.Version != null).Select(d => d.Version);
            var desktopLibVersions = liveDesktopDlls.Where(d => !d.IsRef && d.Version != null).Select(d => d.Version);
            
            // find destkop assemblies with no matching lib.
            var preserveRefVersion = new HashSet<Version>(desktopLibVersions);
            preserveRefVersion.ExceptWith(desktopRefVersions);

            foreach (var extension in s_includedExtensions)
            {
                foreach (var packageFile in Directory.EnumerateFiles(pathToPackage, $"*{extension}", SearchOption.AllDirectories))
                {
                    string packagePath = packageFile.Substring(pathToPackage.Length + 1).Replace('\\', '/');

                    // determine if we should include this file from the harvested package

                    // exclude if its specifically set for exclusion
                    if (ShouldExclude(packagePath))
                    {
                        Log.LogMessage(LogImportance.Low, $"Excluding package path {packagePath}.");
                        continue;
                    }

                    var assemblyVersion = extension == s_dll ? VersionUtility.GetAssemblyVersion(packageFile) : null;
                    PackageItem liveFile = null;

                    // determine if the harvested file clashes with a live built file
                    // we'll prefer the harvested reference assembly so long as it's the same API
                    // version and not required to match implementation 1:1 as is the case for desktop
                    if (livePackageFiles.TryGetValue(packagePath, out liveFile))
                    {
                        // Not a dll, not a ref, or not a versioned assembly: prefer live built file.
                        if (extension != s_dll || !liveFile.IsRef || assemblyVersion == null || liveFile.Version == null)
                        {
                            Log.LogMessage(LogImportance.Low, $"Preferring live build of package path {packagePath} over the asset from last stable package.");
                            continue;
                        }

                        // preserve desktop references to ensure bindingRedirects will work.
                        if (liveFile.TargetFramework.Framework == FrameworkConstants.FrameworkIdentifiers.Net)
                        {
                            Log.LogMessage(LogImportance.Low, $"Preferring live build of package path {packagePath} over the asset from last stable package for desktop framework.");
                            continue;
                        }

                        // as above but handle the case where a netstandard ref may be used for a desktop impl.
                        if (preserveRefVersion.Contains(liveFile.Version))
                        {
                            Log.LogMessage(LogImportance.Low, $"Preferring live build of package path {packagePath} over the asset from last stable package for desktop framework.");
                            continue;
                        }

                        // preserve references with a different major.minor version
                        if (assemblyVersion.Major != liveFile.Version.Major || 
                            assemblyVersion.Minor != liveFile.Version.Minor)
                        {
                            Log.LogMessage(LogImportance.Low, $"Preferring live build of reference {packagePath} over the asset from last stable package since the live build is a different API version.");
                            continue;
                        }

                        // preserve references that specifically set the preserve metadata.
                        bool preserve = false;
                        bool.TryParse(liveFile.OriginalItem.GetMetadata("Preserve"), out preserve);
                        if (preserve)
                        {
                            Log.LogMessage(LogImportance.Low, $"Preferring live build of reference {packagePath} over the asset from last stable package since Preserve was set to true.");
                            continue;
                        }

                        // replace the live file with the harvested one, removing both the live file and PDB from the
                        // file list.
                        Log.LogMessage($"Using reference {packagePath} from last stable package {PackageId}/{PackageVersion} rather than the built reference {liveFile.SourcePath} since it is the same API version.  Set <Preserve>true</Preserve> on {liveFile.SourceProject} if you'd like to avoid this..");
                        removeFiles.Add(liveFile.OriginalItem);

                        PackageItem livePdbFile;
                        if (livePackageFiles.TryGetValue(Path.ChangeExtension(packagePath, ".pdb"), out livePdbFile))
                        {
                            removeFiles.Add(livePdbFile.OriginalItem);
                        }
                    }
                    else
                    {
                        Log.LogMessage(LogImportance.Low, $"Including {packagePath} from last stable package {PackageId}/{PackageVersion}.");
                    }

                    var item = new TaskItem(packageFile);

                    if (liveFile?.OriginalItem != null)
                    {
                        // preserve all the meta-data from the live file that was replaced.
                        liveFile.OriginalItem.CopyMetadataTo(item);
                    }
                    else
                    {
                        var targetPath = Path.GetDirectoryName(packagePath).Replace('\\', '/');
                        item.SetMetadata("TargetPath", targetPath);
                        string targetFramework = GetTargetFrameworkFromPackagePath(targetPath);
                        item.SetMetadata("TargetFramework", targetFramework);
                        // only harvest for non-portable frameworks, matches logic in packaging.targets.
                        bool harvestDependencies = !targetFramework.StartsWith("portable-");
                        item.SetMetadata("HarvestDependencies", harvestDependencies.ToString());
                        item.SetMetadata("IsReferenceAsset", IsReferencePackagePath(targetPath).ToString());
                    }

                    if (assemblyVersion != null)
                    {
                        // overwrite whatever metadata may have been copied from the live file.
                        item.SetMetadata("AssemblyVersion", assemblyVersion.ToString());
                    }

                    item.SetMetadata("HarvestedFrom", $"{PackageId}/{PackageVersion}/{packagePath}");

                    harvestedFiles.Add(item);
                }
            }

            HarvestedFiles = harvestedFiles.ToArray();

            if (Files != null)
            {
                UpdatedFiles = Files.Except(removeFiles).ToArray();
            }
        }

        private string[] _pathsToExclude = null;
        private bool ShouldExclude(string packagePath)
        {
            if (_pathsToExclude == null)
            {
                _pathsToExclude = PathsToExclude.NullAsEmpty().Select(EnsureDirectory).ToArray();
            }

            return ShouldSuppress(packagePath) ||
                _pathsToExclude.Any(p => packagePath.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        private string[] _pathsToSuppress = null;
        private bool ShouldSuppress(string packagePath)
        {
            if (_pathsToSuppress == null)
            {
                _pathsToSuppress = PathsToSuppress.NullAsEmpty().Select(EnsureDirectory).ToArray();
            }

            return _pathsToSuppress.Any(p => packagePath.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        private static string EnsureDirectory(string source)
        {
            string result;

            if (source.Length < 1 || source[source.Length - 1] == '\\' || source[source.Length - 1] == '/')
            {
                // already have a directory
                result = source;
            }
            else
            {
                // could be a directory or file
                var extension = Path.GetExtension(source);

                if (IsIncludedExtension(extension))
                {
                    // it's a file, find the directory portion
                    var fileName = Path.GetFileName(source);
                    if (fileName.Length != source.Length)
                    {
                        result = source.Substring(0, source.Length - fileName.Length);
                    }
                    else
                    {
                        // no directory portion, just return as-is
                        result = source;
                    }
                }
                else
                {
                    // it's a directory, add the slash
                    result = source + '/';
                }
            }

            return result;
        }

        private static string GetTargetFrameworkFromPackagePath(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Length >= 2)
            {
                if (parts[0].Equals("lib", StringComparison.OrdinalIgnoreCase) ||
                    parts[0].Equals("ref", StringComparison.OrdinalIgnoreCase))
                {
                    return parts[1];
                }

                if (parts.Length >= 4 &&
                    parts[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase) &&
                    parts[2].Equals("lib", StringComparison.OrdinalIgnoreCase))
                {
                    return parts[3];
                }
            }

            return null;
        }

        private static string s_dll = ".dll";
        private static string[] s_includedExtensions = new[] { s_dll, ".pdb", ".xml", "._" };
        private static bool IsIncludedExtension(string extension)
        {
            return extension != null && extension.Length > 0 && s_includedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsReferencePackagePath(string path)
        {
            return path.StartsWith("ref", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<string> GetPackageItems(string packagesFolder, string packageId, string packageVersion)
        {
            string packageFolder = Path.Combine(packagesFolder, packageId, packageVersion);

            return GetPackageItems(packageFolder);
        }

        private IEnumerable<string> GetPackageItems(string packageFolder)
        {
            return Directory.EnumerateFiles(packageFolder, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(packageFolder.Length + 1).Replace('\\', '/'))
                .Where(f => !ShouldSuppress(f));
        }
    }
}
