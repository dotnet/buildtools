// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Resolves the assets out of packages in the project.lock.json
    /// </summary>
    public sealed class PrereleaseResolveNuGetPackageAssets : BuildTask
    {
        internal const string NuGetPackageIdMetadata = "NuGetPackageId";
        internal const string NuGetPackageVersionMetadata = "NuGetPackageVersion";
        internal const string NuGetIsFrameworkReference = "NuGetIsFrameworkReference";
        internal const string NuGetSourceType = "NuGetSourceType";
        internal const string NuGetSourceType_Project = "Project";
        internal const string NuGetSourceType_Package = "Package";

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
        private readonly List<ITaskItem> _contentItems = new List<ITaskItem>();
        private readonly List<ITaskItem> _fileWrites = new List<ITaskItem>();

        private readonly List<string> _packageFolders = new List<string>();

        private readonly Dictionary<string, string> _projectReferencesToOutputBasePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #region UnitTestSupport
        private readonly DirectoryExists _directoryExists = new DirectoryExists(Directory.Exists);
        private readonly FileExists _fileExists = new FileExists(File.Exists);
        private readonly TryGetRuntimeVersion _tryGetRuntimeVersion = new TryGetRuntimeVersion(TryGetRuntimeVersion);
        private readonly bool _reportExceptionsToMSBuildLogger = true;

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

            _reportExceptionsToMSBuildLogger = false;
        }

          // For unit testing.
         internal IEnumerable<string> GetPackageFolders()
         {
             return _packageFolders;
         }
 
        #endregion

        /// <summary>
        /// Creates a new <see cref="PrereleaseResolveNuGetPackageAssets"/>.
        /// </summary>
        public PrereleaseResolveNuGetPackageAssets()
        {
            Log.TaskResources = Strings.ResourceManager;
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
        /// Additional content items provided from NuGet packages.
        /// </summary>
        [Output]
        public ITaskItem[] ContentItems => _contentItems.ToArray();

        /// <summary>
        /// Files written to during the generation process.
        /// </summary>
        [Output]
        public ITaskItem[] FileWrites => _fileWrites.ToArray();

        /// <summary>
        /// Items specifying the tokens that can be substituted into preprocessed content files. The ItemSpec of each item is
        /// the name of the token, without the surrounding $$, and the Value metadata should specify the replacement value.
        /// </summary>
        public ITaskItem[] ContentPreprocessorValues
        {
            get; set;
        }

        /// <summary>
        /// A list of project references that are creating packages as listed in the lock file. The OutputPath metadata should
        /// set on each of these items, which is used by the task to construct full output paths to assets.
        /// </summary>
        public ITaskItem[] ProjectReferencesCreatingPackages
        {
            get; set;
        }

        /// <summary>
        /// The base output directory where the temporary, preprocessed files should be written to.
        /// </summary>
        public string ContentPreprocessorOutputDirectory
        {
            get; set;
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

        /// <summary>
        /// Performs the NuGet package resolution.
        /// </summary>
        public override bool Execute()
        {
            try
            {
                ExecuteCore();
                return true;
            }
            catch (ExceptionFromResource e) when (_reportExceptionsToMSBuildLogger)
            {
                Log.LogErrorFromResources(e.ResourceName, e.MessageArgs);
                return false;
            }
            catch (Exception e) when (_reportExceptionsToMSBuildLogger)
            {
                // Any user-visible exceptions we throw should be ExceptionFromResource, so here we should dump stacks because
                // something went very wrong.
                Log.LogErrorFromException(e, showStackTrace: true);
                return false;
            }
        }

        private void ExecuteCore()
        {
            if (!_fileExists(ProjectLockFile))
            {
                throw new ExceptionFromResource(nameof(Strings.LockFileNotFound), ProjectLockFile);
            }

            JObject lockFile;
            using (var streamReader = new StreamReader(new FileStream(ProjectLockFile, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                lockFile = JObject.Load(new JsonTextReader(streamReader));
            }

            PopulatePackageFolders(lockFile);

            PopulateProjectReferenceMaps();
            GetReferences(lockFile);
            GetCopyLocalItems(lockFile);
            GetAnalyzers(lockFile);
            GetReferencedPackages(lockFile);
            ProduceContentAssets(lockFile);
        }

        private void PopulatePackageFolders(JObject lockFile)
        {
            // If we explicitly were given a path, let's use that
            if (!string.IsNullOrEmpty(NuGetPackagesDirectory))
            {
                _packageFolders.Add(NuGetPackagesDirectory);
            }

            // Newer versions of NuGet can now specify the final list of locations in the lock file
            var packageFolders = lockFile["packageFolders"] as JObject;

            if (packageFolders != null)
            {
                foreach (var packageFolder in packageFolders.Properties())
                {
                    _packageFolders.Add(packageFolder.Name);
                }
            }
        }

        private void PopulateProjectReferenceMaps()
        {
            foreach (var projectReference in ProjectReferencesCreatingPackages ?? new ITaskItem[] { })
            {
                var fullPath = GetAbsolutePathFromProjectRelativePath(projectReference.ItemSpec);
                if (_projectReferencesToOutputBasePaths.ContainsKey(fullPath))
                {
                    Log.LogWarningFromResources(nameof(Strings.DuplicateProjectReference), fullPath, nameof(ProjectReferencesCreatingPackages));
                }
                else
                {
                    var outputPath = projectReference.GetMetadata("OutputBasePath");
                    _projectReferencesToOutputBasePaths.Add(fullPath, outputPath);
                }
            }
        }

        private void GetReferences(JObject lockFile)
        {
            var target = GetTargetOrAttemptFallback(lockFile, needsRuntimeIdentifier: false);
            var frameworkReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fileNamesOfRegularReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in GetPackagesFromTarget(lockFile, target))
            {
                foreach (var referenceItem in CreateItems(package, NuGetAssetTypeCompile, includePdbs: false))
                {
                    _references.Add(referenceItem);

                    fileNamesOfRegularReferences.Add(Path.GetFileNameWithoutExtension(referenceItem.ItemSpec));
                }

                if (IncludeFrameworkReferences)
                {
                    var frameworkAssembliesArray = package.TargetObject["frameworkAssemblies"] as JArray;
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
                var item = new TaskItem(frameworkReference);
                item.SetMetadata(NuGetIsFrameworkReference, "true");
                item.SetMetadata(NuGetSourceType, NuGetSourceType_Package);
                _references.Add(item);
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

            foreach (var package in GetPackagesFromTarget(lockFile, target))
            {
                foreach (var nativeItem in CreateItems(package, NuGetAssetTypeNative))
                {
                    if (Path.GetExtension(nativeItem.ItemSpec).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        candidateNativeImplementations.Add(Path.GetFileNameWithoutExtension(nativeItem.ItemSpec));
                    }

                    _copyLocalItems.Add(nativeItem);
                }

                foreach (var runtimeItem in CreateItems(package, NuGetAssetTypeRuntime))
                {
                    if (Path.GetExtension(runtimeItem.ItemSpec).Equals(".winmd", StringComparison.OrdinalIgnoreCase))
                    {
                        runtimeWinMDItems.Add(runtimeItem);
                    }

                    _copyLocalItems.Add(runtimeItem);
                }

                foreach (var resourceItem in CreateItems(package, NuGetAssetTypeResource))
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

            foreach (var package in GetPackagesFromTarget(lockFile, target))
            {
                var files = package.LibraryObject["files"];

                if (files != null)
                {
                    foreach (var file in files.Children()
                                        .Select(x => x.ToString())
                                        .Where(x => x.StartsWith("analyzers")))
                    {
                        if (Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            string path;
                            if (TryGetFile(package.Id, package.Version, file, out path))
                            {
                                var analyzer = new TaskItem(path);

                                analyzer.SetMetadata(NuGetPackageIdMetadata, package.Id);
                                analyzer.SetMetadata(NuGetPackageVersionMetadata, package.Version);

                                _analyzers.Add(analyzer);
                            }
                        }
                    }
                }
            }
        }

        private void SetWinMDMetadata(IEnumerable<ITaskItem> runtimeWinMDs, ICollection<string> candidateImplementations)
        {
            foreach (var winMD in runtimeWinMDs.Where(w => _fileExists(w.ItemSpec)))
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
        /// Produces a string hash of the key/values in the dictionary. This hash is used to put all the
        /// preprocessed files into a folder with the name so we know to regenerate when any of the
        /// inputs change.
        /// </summary>
        private string BuildPreprocessedContentHash(IReadOnlyDictionary<string, string> values)
        {
            using (var stream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
                {
                    foreach (var pair in values.OrderBy(v => v.Key))
                    {
                        streamWriter.Write(pair.Key);
                        streamWriter.Write('\0');
                        streamWriter.Write(pair.Value);
                        streamWriter.Write('\0');
                    }
                }

                stream.Position = 0;

                return SHA1.Create().ComputeHash(stream).Aggregate("", (s, b) => s + b.ToString("x2"));
            }
        }

        private void ProduceContentAssets(JObject lockFile)
        {
            string valueSpecificPreprocessedOutputDirectory = null;
            var preprocessorValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // If a preprocessor directory isn't set, then we won't have a place to generate.
            if (!string.IsNullOrEmpty(ContentPreprocessorOutputDirectory))
            {
                // Assemble the preprocessor values up-front
                var duplicatedPreprocessorKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var preprocessorValueItem in ContentPreprocessorValues ?? Enumerable.Empty<ITaskItem>())
                {
                    if (preprocessorValues.ContainsKey(preprocessorValueItem.ItemSpec))
                    {
                        duplicatedPreprocessorKeys.Add(preprocessorValueItem.ItemSpec);
                    }

                    preprocessorValues[preprocessorValueItem.ItemSpec] = preprocessorValueItem.GetMetadata("Value");
                }

                foreach (var duplicatedPreprocessorKey in duplicatedPreprocessorKeys)
                {
                    Log.LogWarningFromResources(nameof(Strings.DuplicatePreprocessorToken), duplicatedPreprocessorKey, preprocessorValues[duplicatedPreprocessorKey]);
                }

                valueSpecificPreprocessedOutputDirectory = Path.Combine(ContentPreprocessorOutputDirectory, BuildPreprocessedContentHash(preprocessorValues));
            }

            // For shared content, it does not depend upon the RID so we should ignore it
            var target = GetTargetOrAttemptFallback(lockFile, needsRuntimeIdentifier: false);

            foreach (var package in GetPackagesFromTarget(lockFile, target))
            {
                var contentFiles = package.TargetObject["contentFiles"] as JObject;
                if (contentFiles != null)
                {
                    // Is there an asset with our exact language? If so, we use that. Otherwise we'll simply collect "any" assets.
                    string codeLanguageToSelect;

                    if (string.IsNullOrEmpty(ProjectLanguage))
                    {
                        codeLanguageToSelect = "any";
                    }
                    else
                    {
                        string nuGetLanguageName = GetNuGetLanguageName(ProjectLanguage);
                        if (contentFiles.Properties().Any(a => (string)a.Value["codeLanguage"] == nuGetLanguageName))
                        {
                            codeLanguageToSelect = nuGetLanguageName;
                        }
                        else
                        {
                            codeLanguageToSelect = "any";
                        }
                    }

                    foreach (var contentFile in contentFiles.Properties())
                    {
                        // Ignore magic _._ placeholder files. We couldn't ignore them during the project language
                        // selection, since you could imagine somebody might have a package that puts assets under
                        // "any" but then uses _._ to opt some languages out of it
                        if (Path.GetFileName(contentFile.Name) != "_._")
                        {
                            if ((string)contentFile.Value["codeLanguage"] == codeLanguageToSelect)
                            {
                                ProduceContentAsset(package, contentFile, preprocessorValues, valueSpecificPreprocessedOutputDirectory);
                            }
                        }
                    }
                }
            }
        }

        private static string GetNuGetLanguageName(string projectLanguage)
        {
            switch (projectLanguage)
            {
                case "C#": return "cs";
                case "F#": return "fs";
                default: return projectLanguage.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Produces the asset for a single shared asset. All applicablility checks have already been completed.
        /// </summary>
        private void ProduceContentAsset(NuGetPackageObject package, JProperty sharedAsset, IReadOnlyDictionary<string, string> preprocessorValues, string preprocessedOutputDirectory)
        {
            string pathToFinalAsset = package.GetFullPathToFile(sharedAsset.Name);

            if (sharedAsset.Value["ppOutputPath"] != null)
            {
                if (preprocessedOutputDirectory == null)
                {
                    throw new ExceptionFromResource(nameof(Strings.PreprocessedDirectoryNotSet), nameof(ContentPreprocessorOutputDirectory));
                }

                // We need the preprocessed output, so let's run the preprocessor here
                pathToFinalAsset = Path.Combine(preprocessedOutputDirectory, package.Id, package.Version, (string)sharedAsset.Value["ppOutputPath"]);

                if (!File.Exists(pathToFinalAsset))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(pathToFinalAsset));

                    using (var input = new StreamReader(new FileStream(package.GetFullPathToFile(sharedAsset.Name), FileMode.Open, FileAccess.Read, FileShare.Read), detectEncodingFromByteOrderMarks: true))
                    using (var output = new StreamWriter(new FileStream(pathToFinalAsset, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write), encoding: input.CurrentEncoding))
                    {
                        Preprocessor.Preprocess(input, output, preprocessorValues);
                    }

                    _fileWrites.Add(new TaskItem(pathToFinalAsset));
                }
            }

            if ((bool)sharedAsset.Value["copyToOutput"])
            {
                string outputPath = (string)sharedAsset.Value["outputPath"] ?? (string)sharedAsset.Value["ppOutputPath"];

                if (outputPath != null)
                {
                    var item = CreateItem(package, pathToFinalAsset, targetPath: outputPath);
                    _copyLocalItems.Add(item);
                }
            }

            string buildAction = (string)sharedAsset.Value["buildAction"];
            if (!string.Equals(buildAction, "none", StringComparison.OrdinalIgnoreCase))
            {
                var item = CreateItem(package, pathToFinalAsset);

                // We'll put additional metadata on the item so we can convert it back to the real item group in our targets
                item.SetMetadata("NuGetItemType", buildAction);

                // If this is XAML, the build targets expect Link metadata to construct the relative path
                if (string.Equals(buildAction, "Page", StringComparison.OrdinalIgnoreCase))
                {
                    item.SetMetadata("Link", Path.Combine("NuGet", package.Id, package.Version, Path.GetFileName(sharedAsset.Name)));
                }

                _contentItems.Add(item);
            }
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

            // If we need a runtime identifier, let's see if we have the framework targeted. If we do,
            // then we can give a better error message.
            bool onlyNeedsRuntimeInProjectJson = false;
            if (needsRuntimeIdentifier)
            {
                foreach (var targetMoniker in TargetMonikers)
                {
                    var targetMonikerWithoutRuntimeIdentifier = GetTargetMonikerWithOptionalRuntimeIdentifier(targetMoniker, needsRuntimeIdentifier: false);
                    if (targets[targetMonikerWithoutRuntimeIdentifier] != null)
                    {
                        // We do have a TXM being targeted, so we just are missing the runtime
                        onlyNeedsRuntimeInProjectJson = true;
                        break;
                    }
                }
            }

            if (onlyNeedsRuntimeInProjectJson)
            {
                GiveErrorForMissingRuntimeIdentifier();
            }
            else
            {
                ThrowExceptionIfNotAllowingFallback(nameof(Strings.MissingFramework), TargetMonikers.First().ItemSpec);
            }

            // If we're still here, that means we're allowing fallback, so let's try
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
                throw new ExceptionFromResource(nameof(Strings.NoTargetsInLockFile));
            }

            return firstTarget;
        }

        private void GiveErrorForMissingRuntimeIdentifier()
        {
            string runtimePiece = '"' + RuntimeIdentifier + "\": { }";

            bool hasRuntimesSection;
            try
            {
                using (var streamReader = new StreamReader(new FileStream(ProjectLockFile.Replace(".lock.json", ".json"), FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    var jsonFile = JObject.Load(new JsonTextReader(streamReader));
                    hasRuntimesSection = jsonFile["runtimes"] != null;
                }
            }
            catch
            {
                // User has a bad file, locked file, no file at all, etc. We'll just assume they have one.
                hasRuntimesSection = true;
            }

            if (hasRuntimesSection)
            {
                ThrowExceptionIfNotAllowingFallback(nameof(Strings.MissingRuntimeInRuntimesSection), RuntimeIdentifier, runtimePiece);
            }
            else
            {
                var runtimesSection = "\"runtimes\": { " + runtimePiece + " }";
                ThrowExceptionIfNotAllowingFallback(nameof(Strings.MissingRuntimesSection), runtimesSection);
            }
        }

        private void ThrowExceptionIfNotAllowingFallback(string resourceName, params string[] messageArgs)
        {
            if (!AllowFallbackOnTargetSelection)
            {
                throw new ExceptionFromResource(resourceName, messageArgs);
            }
            else
            {
                // We are allowing fallback, so we'll still give a warning but allow us to continue
                Log.LogWarningFromResources(resourceName, messageArgs);
            }
        }

        private string GetTargetMonikerWithOptionalRuntimeIdentifier(ITaskItem preferredTargetMoniker, bool needsRuntimeIdentifier)
        {
            return needsRuntimeIdentifier ? preferredTargetMoniker.ItemSpec + "/" + RuntimeIdentifier : preferredTargetMoniker.ItemSpec;
        }

        private IEnumerable<ITaskItem> CreateItems(NuGetPackageObject package, string key, bool includePdbs = true)
        {
            var values = package.TargetObject[key] as JObject;
            var items = new List<ITaskItem>();

            if (values == null)
            {
                return items;
            }

            foreach (var file in values.Properties())
            {
                if (Path.GetFileName(file.Name) == "_._")
                {
                    continue;
                }

                string targetPath = null;
                string culture = file.Value["locale"]?.ToString();

                if (culture != null)
                {
                    targetPath = Path.Combine(culture, Path.GetFileName(file.Name));
                }

                var item = CreateItem(package, package.GetFullPathToFile(file.Name), targetPath);

                item.SetMetadata("Private", "false");
                item.SetMetadata(NuGetIsFrameworkReference, "false");
                item.SetMetadata(NuGetSourceType, package.IsProject ? NuGetSourceType_Project : NuGetSourceType_Package);

                items.Add(item);

                // If there's a PDB alongside the implementation, we should copy that as well
                if (includePdbs)
                {
                    var pdbFileName = Path.ChangeExtension(item.ItemSpec, ".pdb");

                    if (_fileExists(pdbFileName))
                    {
                        var pdbItem = new TaskItem(pdbFileName);

                        // CopyMetadataTo also includes an OriginalItemSpec that will point to our original item, as we want
                        item.CopyMetadataTo(pdbItem);

                        items.Add(pdbItem);
                    }
                }
            }

            return items;
        }

        private static ITaskItem CreateItem(NuGetPackageObject package, string itemSpec, string targetPath = null)
        {
            var item = new TaskItem(itemSpec);

            item.SetMetadata(NuGetPackageIdMetadata, package.Id);
            item.SetMetadata(NuGetPackageVersionMetadata, package.Version);

            if (targetPath != null)
            {
                item.SetMetadata("TargetPath", targetPath);

                var destinationSubDirectory = Path.GetDirectoryName(targetPath);

                if (!string.IsNullOrEmpty(destinationSubDirectory))
                {
                    item.SetMetadata("DestinationSubDirectory", destinationSubDirectory + "\\");
                }
            }

            return item;
        }

        private void GetReferencedPackages(JObject lockFile)
        {
            var projectFileDependencyGroups = (JObject)lockFile["projectFileDependencyGroups"];

            // find whichever target we will have selected
            var actualTarget = GetTargetOrAttemptFallback(lockFile, needsRuntimeIdentifier: false)?.Parent as JProperty;
            string targetMoniker = null;
            if (actualTarget != null)
            {
                targetMoniker = actualTarget.Name.Split('/').FirstOrDefault();
            }

            foreach (var dependencyGroup in projectFileDependencyGroups.Values<JProperty>())
            {
                if (dependencyGroup.Name.Length == 0 || dependencyGroup.Name == targetMoniker)
                {
                    foreach (var packageDependency in dependencyGroup.Value.Values<string>())
                    {
                        int firstSpace = packageDependency.IndexOf(' ');

                        if (firstSpace > -1)
                        {
                            _referencedPackages.Add(new TaskItem(packageDependency.Substring(0, firstSpace)));
                        }
                    }
                }
            }
        }

        private string GetNuGetPackagePath(string packageId, string packageVersion)
        {
            foreach (var packagesFolder in _packageFolders)
            {
                string packagePath = Path.Combine(packagesFolder, packageId, packageVersion);

                if (_directoryExists(packagePath))
                {
                    return packagePath;
                }
                else
                {
                    // Try a lower-case path.
                    string lowerCasePath = Path.Combine(packagesFolder, packageId.ToLowerInvariant(), packageVersion.ToLowerInvariant());
                    if (_directoryExists(lowerCasePath))
                    {
                        return lowerCasePath;
                    }
                }
            }

           throw new ExceptionFromResource(nameof(Strings.PackageFolderNotFound), packageId, packageVersion,
                        string.Join(", ", _packageFolders));
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

        private IEnumerable<NuGetPackageObject> GetPackagesFromTarget(JObject lockFile, JObject target)
        {
            foreach (var package in target)
            {
                var nameParts = package.Key.Split('/');
                var id = nameParts[0];
                var version = nameParts[1];
                bool isProject = false;

                var libraryObject = (JObject)lockFile["libraries"][package.Key];

                Func<string> fullPackagePathGenerator;

                // If this is a project then we need to figure out it's relative output path
                if ((string)libraryObject["type"] == "project")
                {
                    isProject = true;

                    fullPackagePathGenerator = () =>
                    {
                        var relativeMSBuildProjectPath = (string)libraryObject["msbuildProject"];

                        if (string.IsNullOrEmpty(relativeMSBuildProjectPath))
                        {
                            throw new ExceptionFromResource(nameof(Strings.MissingMSBuildPathInProjectPackage), id);
                        }

                        var absoluteMSBuildProjectPath = GetAbsolutePathFromProjectRelativePath(relativeMSBuildProjectPath);
                        string fullPackagePath;
                        if (!_projectReferencesToOutputBasePaths.TryGetValue(absoluteMSBuildProjectPath, out fullPackagePath))
                        {
                            throw new ExceptionFromResource(nameof(Strings.MissingProjectReference), absoluteMSBuildProjectPath, nameof(ProjectReferencesCreatingPackages));
                        }

                        return fullPackagePath;
                    };
                }
                else
                {
                    fullPackagePathGenerator = () => GetNuGetPackagePath(id, version);
                }

                yield return new NuGetPackageObject(id, version, isProject, fullPackagePathGenerator, (JObject)package.Value, libraryObject);
            }
        }

        private string GetAbsolutePathFromProjectRelativePath(string path)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ProjectLockFile)), path));
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
