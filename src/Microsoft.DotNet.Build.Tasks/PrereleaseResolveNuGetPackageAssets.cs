// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Resolves the assets out of packages in the project.lock.json
    /// </summary>
    public sealed class PrereleaseResolveNuGetPackageAssets : Task
    {
        internal const string NuGetPackageIdMetadata = "NuGetPackageId";
        internal const string NuGetPackageVersionMetadata = "NuGetPackageVersion";
        internal const string ReferenceImplementationMetadata = "Implementation";
        internal const string ReferenceImageRuntimeMetadata = "ImageRuntime";
        internal const string ReferenceWinMDFileMetadata = "WinMDFile";
        internal const string ReferenceWinMDFileTypeMetadata = "WinMDFileType";
        internal const string WinMDFileTypeManaged = "Managed";
        internal const string WinMDFileTypeNative = "Native";
        internal const string NuGetAssetTypeCompile = "compile";
        internal const string NuGetAssetTypeNative = "native";
        internal const string NuGetAssetTypeRuntime = "runtime";
        internal const string NuGetAssetTypeResource = "resource";


        private readonly List<ITaskItem> _analyzers = new List<ITaskItem>();
        private readonly List<ITaskItem> _copyLocalItems = new List<ITaskItem>();
        private readonly List<ITaskItem> _references = new List<ITaskItem>();
        private readonly List<ITaskItem> _referencedPackages = new List<ITaskItem>();

        #region UnitTestSupport
        private readonly DirectoryExists _directoryExists = new DirectoryExists(Directory.Exists);
        private readonly FileExists _fileExists = new FileExists(File.Exists);
        private readonly TryGetRuntimeVersion _tryGetRuntimeVersion = new TryGetRuntimeVersion(TryGetRuntimeVersion);

        internal PrereleaseResolveNuGetPackageAssets(DirectoryExists directoryExists, FileExists fileExists, TryGetRuntimeVersion tryGetRuntimeVersion)
            : this()
        {
            if (directoryExists != null)
            {
                _directoryExists = directoryExists;
            }

            if (fileExists != null)
            {
                _fileExists = fileExists;
            }

            if (tryGetRuntimeVersion != null)
            {
                _tryGetRuntimeVersion = tryGetRuntimeVersion;
            }
        }
        #endregion

        /// <summary>
        /// Creates a new <see cref="PrereleaseResolveNuGetPackageAssets"/>.
        /// </summary>
        public PrereleaseResolveNuGetPackageAssets()
        {
        }

        /// <summary>
        /// The full paths to resolved analyzers.
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedAnalyzers
        {
            get { return _analyzers.ToArray(); }
        }

        /// <summary>
        /// The full paths to resolved run-time resources.
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedCopyLocalItems
        {
            get { return _copyLocalItems.ToArray(); }
        }

        /// <summary>
        /// The full paths to resolved build-time dependencies. Contains standard metadata for Reference items.
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedReferences
        {
            get { return _references.ToArray(); }
        }

        /// <summary>
        /// The names of NuGet packages directly referenced by this project.
        /// </summary>
        [Output]
        public ITaskItem[] ReferencedPackages
        {
            get { return _referencedPackages.ToArray(); }
        }

        /// <summary>
        /// The target monikers to use when selecting assets from packages. The first one found in the lock file is used.
        /// </summary>
        [Required]
        public ITaskItem[] TargetMonikers
        {
            get; set;
        }

        [Required]
        public string ProjectLockFile
        {
            get; set;
        }

        public string NuGetPackagesDirectory
        {
            get; set;
        }

        public string RuntimeIdentifier
        {
            get; set;
        }

        public bool AllowFallbackOnTargetSelection
        {
            get; set;
        }

        public string ProjectLanguage
        {
            get; set;
        }

        public bool IncludeFrameworkReferences
        {
            get; set;
        }

        public bool OmitTransitiveCompileReferences
        {
            get; set;
        }

        /// <summary>
        /// Performs the NuGet package resolution.
        /// </summary>
        public override bool Execute()
        {
            Log.TaskResources = Strings.ResourceManager;

            try
            {
                ExecuteCore();
                return true;
            }
            catch (ExceptionFromResource e)
            {
                Log.LogErrorFromResources(e.ResourceName, e.MessageArgs);
                return false;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                return false;
            }
        }

        private void ExecuteCore()
        {
            if (!_fileExists(ProjectLockFile))
            {
                throw new ExceptionFromResource("LockFileNotFound", ProjectLockFile);
            }

            JObject lockFile;
            using (var streamReader = new StreamReader(new FileStream(ProjectLockFile, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                lockFile = JObject.Load(new JsonTextReader(streamReader));
            }
            
            GetReferences(lockFile);
            GetCopyLocalItems(lockFile);
            GetAnalyzers(lockFile);
            GetReferencedPackages(lockFile);
        }

        private void GetReferences(JObject lockFile)
        {
            var target = GetTargetOrAttemptFallback(lockFile, needsRuntimeIdentifier: false);
            var frameworkReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fileNamesOfRegularReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            HashSet<string> directReferences = new HashSet<string>();
            if (OmitTransitiveCompileReferences)
            {
                directReferences.UnionWith(GetDirectReferences(lockFile));
            }

            foreach (var package in target)
            {
                var packageNameParts = package.Key.Split('/');
                var packageName = packageNameParts[0];
                var packageVersion = packageNameParts[1];


                if (OmitTransitiveCompileReferences && !directReferences.Contains(packageName))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "OmitReferencesFromIndirectPackage", packageName);
                    continue;
                }

                Log.LogMessageFromResources(MessageImportance.Low, "ResolvedReferencesFromPackage", packageName);

                foreach (var referenceItem in CreateItems(packageName, packageVersion, package.Value, NuGetAssetTypeCompile))
                {
                    _references.Add(referenceItem);

                    fileNamesOfRegularReferences.Add(Path.GetFileNameWithoutExtension(referenceItem.ItemSpec));
                }

                if (IncludeFrameworkReferences)
                {
                    var frameworkAssembliesArray = package.Value["frameworkAssemblies"] as JArray;
                    if (frameworkAssembliesArray != null)
                    {
                        foreach (var frameworkAssembly in frameworkAssembliesArray.OfType<JToken>())
                        {
                            frameworkReferences.Add((string)frameworkAssembly);
                        }
                    }
                }
            }

            foreach (var frameworkReference in frameworkReferences.Except(fileNamesOfRegularReferences, StringComparer.OrdinalIgnoreCase))
            {
                _references.Add(new TaskItem(frameworkReference));
            }
        }

        private void GetCopyLocalItems(JObject lockFile)
        {
            // If we have no runtime identifier, we're not copying implementations
            if (string.IsNullOrEmpty(RuntimeIdentifier))
            {
                return;
            }

            // We'll use as a fallback just the target moniker if the user didn't have the right runtime identifier in their lock file.
            var target = GetTargetOrAttemptFallback(lockFile, needsRuntimeIdentifier: true);

            HashSet<string> candidateNativeImplementations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<ITaskItem> runtimeWinMDItems = new List<ITaskItem>();

            foreach (var package in target)
            {
                var packageNameParts = package.Key.Split('/');
                var packageName = packageNameParts[0];
                var packageVersion = packageNameParts[1];

                Log.LogMessageFromResources(MessageImportance.Low, "ResolvedReferencesFromPackage", packageName);

                foreach(var nativeItem in CreateItems(packageName, packageVersion, package.Value, NuGetAssetTypeNative))
                {
                    if (Path.GetExtension(nativeItem.ItemSpec).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        candidateNativeImplementations.Add(Path.GetFileNameWithoutExtension(nativeItem.ItemSpec));
                    }

                    _copyLocalItems.Add(nativeItem);
                }

                foreach (var runtimeItem in CreateItems(packageName, packageVersion, package.Value, NuGetAssetTypeRuntime))
                {
                    if (Path.GetExtension(runtimeItem.ItemSpec).Equals(".winmd", StringComparison.OrdinalIgnoreCase))
                    {
                        runtimeWinMDItems.Add(runtimeItem);
                    }

                    _copyLocalItems.Add(runtimeItem);
                }

                foreach (var resourceItem in CreateItems(packageName, packageVersion, package.Value, NuGetAssetTypeResource))
                {
                    _copyLocalItems.Add(resourceItem);
                }
            }

            SetWinMDMetadata(runtimeWinMDItems, candidateNativeImplementations);
        }

        private void GetAnalyzers(JObject lockFile)
        {
            // For analyzers, analyzers could be provided in runtime implementation packages. This might be reasonable -- imagine a gatekeeper
            // scenario where somebody has a library but on .NET Native might have some specific restrictions that need to be enforced.
            var target = GetTargetOrAttemptFallback(lockFile, needsRuntimeIdentifier: !string.IsNullOrEmpty(RuntimeIdentifier));

            var libraries = (JObject)lockFile["libraries"];

            foreach (var package in target.Children())
            {
                var name = (package is JProperty) ? ((JProperty)package).Name : null;
                var packageNameParts = name != null ? name.Split('/') : null;
                if (packageNameParts == null)
                {
                    continue;
                }

                var packageId = packageNameParts[0];
                var packageVersion = packageNameParts[1];

                var librariesPackage = libraries[name];

                foreach (var file in librariesPackage["files"].Children()
                                    .Select(x => x.ToString())
                                    .Where(x => x.StartsWith("analyzers")))
                {
                    if (Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        string path;
                        if (TryGetFile(packageId, packageVersion, file, out path))
                        {
                            var analyzer = new TaskItem(path);

                            analyzer.SetMetadata(NuGetPackageIdMetadata, packageId);
                            analyzer.SetMetadata(NuGetPackageVersionMetadata, packageVersion);

                            _analyzers.Add(analyzer);
                        }
                    }
                }
            }
        }

        private void SetWinMDMetadata(IEnumerable<ITaskItem> runtimeWinMDs, ICollection<string> candidateImplementations)
        {
            foreach(var winMD in runtimeWinMDs.Where(w => _fileExists(w.ItemSpec)))
            {
                string imageRuntimeVersion = _tryGetRuntimeVersion(winMD.ItemSpec);

                if (String.IsNullOrEmpty(imageRuntimeVersion))
                    continue;

                // RAR sets ImageRuntime for everything but the only dependencies we're aware of are 
                // for WinMDs
                winMD.SetMetadata(ReferenceImageRuntimeMetadata, imageRuntimeVersion);

                bool isWinMD, isManaged;
                TryParseRuntimeVersion(imageRuntimeVersion, out isWinMD, out isManaged);

                if (isWinMD)
                {
                    winMD.SetMetadata(ReferenceWinMDFileMetadata, "true");

                    if (isManaged)
                    {
                        winMD.SetMetadata(ReferenceWinMDFileTypeMetadata, WinMDFileTypeManaged);
                    }
                    else
                    {
                        winMD.SetMetadata(ReferenceWinMDFileTypeMetadata, WinMDFileTypeNative);

                        // Normally RAR will expect the native DLL to be next to the WinMD, but that doesn't
                        // work well for nuget packages since compile time assets cannot be architecture specific.
                        // We also explicitly set all compile time assets to not copy local so we need to 
                        // make sure that this metadata is set on the runtime asset.

                        // Examine all runtime assets that are native winmds and add Implementation metadata
                        // We intentionally permit this to cross package boundaries to support cases where
                        // folks want to split their architecture specific implementations into runtime
                        // specific packages.

                        // Sample layout            
                        // lib\netcore50\Contoso.Controls.winmd
                        // lib\netcore50\Contoso.Controls.xml
                        // runtimes\win10-arm\native\Contoso.Controls.dll
                        // runtimes\win10-x64\native\Contoso.Controls.dll
                        // runtimes\win10-x86\native\Contoso.Controls.dll

                        string fileName = Path.GetFileNameWithoutExtension(winMD.ItemSpec);

                        // determine if we have a Native WinMD that could be satisfied by this native dll.
                        if (candidateImplementations.Contains(fileName))
                        {
                            winMD.SetMetadata(ReferenceImplementationMetadata, fileName + ".dll");
                        }
                    }
                }
            }
        }

        private bool TryGetFile(string packageName, string packageVersion, string file, out string path)
        {
            if (IsFileValid(file, "C#", "VB"))
            {
                path = GetPath(packageName, packageVersion, file);
                return true;
            }
            else if (IsFileValid(file, "VB", "C#"))
            {
                path = GetPath(packageName, packageVersion, file);
                return true;
            }

            path = null;
            return false;
        }

        private bool IsFileValid(string file, string expectedLanguage, string unExpectedLanguage)
        {
            var expectedProjectLanguage = expectedLanguage;
            expectedLanguage = expectedLanguage == "C#" ? "cs" : expectedLanguage;
            unExpectedLanguage = unExpectedLanguage == "C#" ? "cs" : unExpectedLanguage;

            return (ProjectLanguage.Equals(expectedProjectLanguage, StringComparison.OrdinalIgnoreCase)) &&
                            (file.Split('/').Any(x => x.Equals(ProjectLanguage, StringComparison.OrdinalIgnoreCase)) ||
                            !file.Split('/').Any(x => x.Equals(unExpectedLanguage, StringComparison.OrdinalIgnoreCase)));
        }

        private string GetPath(string packageName, string packageVersion, string file)
        {
            return Path.Combine(GetNuGetPackagePath(packageName, packageVersion), file.Replace('/', '\\'));
        }

        /// <summary>
        /// Fetches the right target from the targets section in a lock file, or attempts to find a "best match" if allowed. The "best match" logic
        /// is there to allow a design time build for the IDE to generally work even if something isn't quite right. Throws an exception
        /// if either the preferred isn't there and fallbacks aren't allowed, or fallbacks are allowed but nothing at all could be found.
        /// </summary>
        /// <param name="lockFile">The lock file JSON.</param>
        /// <param name="needsRuntimeIdentifier">Whether we must find targets that include the runtime identifier or one without the runtime identifier.</param>
        private JObject GetTargetOrAttemptFallback(JObject lockFile, bool needsRuntimeIdentifier)
        {
            var targets = (JObject)lockFile["targets"];

            foreach (var preferredTargetMoniker in TargetMonikers)
            {
                var preferredTargetMonikerWithOptionalRuntimeIdentifier = GetTargetMonikerWithOptionalRuntimeIdentifier(preferredTargetMoniker, needsRuntimeIdentifier);
                var target = (JObject)targets[preferredTargetMonikerWithOptionalRuntimeIdentifier];

                if (target != null)
                {
                    return target;
                }
            }

            var preferredForErrorMessages = GetTargetMonikerWithOptionalRuntimeIdentifier(TargetMonikers.First(), needsRuntimeIdentifier);
            if (!AllowFallbackOnTargetSelection)
            {
                // If we're not falling back then abort the build
                throw new ExceptionFromResource("MissingEntryInLockFile", preferredForErrorMessages);
            }

            // We are allowing fallback, so we'll still give a warning but allow us to continue
            // In production ResolveNuGetPackageAssets, this call is LogWarningFromResources.
            // In our current use in dotnet\buildtools, we rely on the fallback behavior, so we just log
            // this as a message.
            Log.LogMessageFromResources("MissingEntryInLockFile", preferredForErrorMessages);

            foreach (var fallback in TargetMonikers)
            {
                var target = (JObject)targets[GetTargetMonikerWithOptionalRuntimeIdentifier(fallback, needsRuntimeIdentifier: false)];

                if (target != null)
                {
                    return target;
                }
            }

            // Anything goes
            var enumerableTargets = targets.Cast<KeyValuePair<string, JToken>>();
            var firstTarget = (JObject)enumerableTargets.FirstOrDefault().Value;
            if (firstTarget == null)
            {
                throw new ExceptionFromResource("NoTargetsInLockFile");
            }

            return firstTarget;
        }


        /// <summary>
        /// Determines the packages IDs that were directly referenced
        /// </summary>
        /// <param name="lockFile">The lock file JSON.</param>
        private IEnumerable<string> GetDirectReferences(JObject lockFile)
        {
            var dependencyGroups = (JObject)lockFile["projectFileDependencyGroups"];

            if (null == dependencyGroups)
            {
                return Enumerable.Empty<string>();
            }

            return dependencyGroups.Values<JProperty>()
                .Where(dg => dg.Name == "" || TargetMonikers.Select(tm => tm.ItemSpec).Contains(dg.Name))
                .SelectMany(dg => dg.Value.Values<string>())
                .Select(dependencyClause =>
                {
                    int lengthOfDependencyId = dependencyClause.IndexOf(' ');

                    if (lengthOfDependencyId == -1)
                    {
                        throw new Exception("InvalidDependencyFormat");
                    }

                    return dependencyClause.Substring(0, lengthOfDependencyId);
                });
        }

        private string GetTargetMonikerWithOptionalRuntimeIdentifier(ITaskItem preferredTargetMoniker, bool needsRuntimeIdentifier)
        {
            return needsRuntimeIdentifier ? preferredTargetMoniker.ItemSpec + "/" + RuntimeIdentifier : preferredTargetMoniker.ItemSpec;
        }

        private IEnumerable<ITaskItem> CreateItems(string packageId, string packageVersion, JToken packageObject, string key)
        {
            var values = packageObject[key] as JObject;
            var items = new List<ITaskItem>();

            if (values == null)
            {
                return items;
            }

            var nugetPackage = GetNuGetPackagePath(packageId, packageVersion);

            foreach (string file in values.Properties().Select(p => p.Name))
            {
                if (Path.GetFileName(file) == "_._")
                {
                    continue;
                }

                var sanitizedFile = file.Replace('/', '\\');
                var nugetPath = Path.Combine(nugetPackage, sanitizedFile);
                var item = new TaskItem(nugetPath);

                item.SetMetadata(NuGetPackageIdMetadata, packageId);
                item.SetMetadata(NuGetPackageVersionMetadata, packageVersion);
                item.SetMetadata("Private", "false");

                string targetPath = TryGetTargetPath(sanitizedFile);

                if (targetPath != null)
                {
                    var destinationSubDirectory = Path.GetDirectoryName(targetPath);

                    if (!string.IsNullOrEmpty(destinationSubDirectory))
                    {
                        item.SetMetadata("DestinationSubDirectory", destinationSubDirectory + "\\");
                    }

                    item.SetMetadata("TargetPath", targetPath);
                }

                items.Add(item);
            }

            return items;
        }

        private static string TryGetTargetPath(string file)
        {
            var foldersAndFile = file.Split('\\').ToArray();
#if TODO // Not sure if we support culture specific directories yet...
            for (int i = foldersAndFile.Length - 1; i > -1; i--)
            {
                if (CultureStringUtilities.IsValidCultureString(foldersAndFile[i]))
                {
                    return Path.Combine(foldersAndFile.Skip(i).ToArray());
                }
            }
#endif
            // There is no culture-specific directory, so it'll go in the root
            return null;
        }

        private void GetReferencedPackages(JObject lockFile)
        {
            var projectFileDependencyGroups = (JObject)lockFile["projectFileDependencyGroups"];
            var projectFileDependencies = (JArray)projectFileDependencyGroups[""];

            foreach (var packageDependency in projectFileDependencies.Select(v => (string)v))
            {
                int firstSpace = packageDependency.IndexOf(' ');

                if (firstSpace > -1)
                {
                    _referencedPackages.Add(new TaskItem(packageDependency.Substring(0, firstSpace)));
                }
            }
        }

        private sealed class ExceptionFromResource : Exception
        {
            public string ResourceName { get; private set; }
            public object[] MessageArgs { get; private set; }

            public ExceptionFromResource(string resourceName, params object[] messageArgs)
            {
                ResourceName = resourceName;
                MessageArgs = messageArgs;
            }
        }

        private string GetNuGetPackagePath(string packageId, string packageVersion)
        {
            string packagesFolder = GetNuGetPackagesPath();
            string packagePath = Path.Combine(packagesFolder, packageId, packageVersion);

            if (!_directoryExists(packagePath))
            {
                throw new ExceptionFromResource("PackageFolderNotFound", packageId, packageVersion, packagesFolder);
            }

            return packagePath;
        }

        private string GetNuGetPackagesPath()
        {
            if (!string.IsNullOrEmpty(NuGetPackagesDirectory))
            {
                return NuGetPackagesDirectory;
            }

            string packagesFolder = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

            if (!string.IsNullOrEmpty(packagesFolder))
            {
                return packagesFolder;
            }

            return string.Empty;
        }
        
        /// <summary>
        /// Parse the imageRuntimeVersion from COR header
        /// </summary>
        private void TryParseRuntimeVersion(string imageRuntimeVersion, out bool isWinMD, out bool isManaged)
        {
            if (!String.IsNullOrEmpty(imageRuntimeVersion))
            {
                isWinMD = imageRuntimeVersion.IndexOf("WindowsRuntime", StringComparison.OrdinalIgnoreCase) >= 0;
                isManaged = imageRuntimeVersion.IndexOf("CLR", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            else
            {
                isWinMD = isManaged = false;
            }
        }

        /// <summary>
        /// Given a path get the CLR runtime version of the file
        /// </summary>
        /// <param name="path">path to the file</param>
        /// <returns>The CLR runtime version or empty if the path does not exist.</returns>
        private static string TryGetRuntimeVersion(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (PEReader peReader = new PEReader(stream))
                {
                    return peReader.GetMetadataReader().MetadataVersion;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
