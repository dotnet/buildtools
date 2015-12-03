// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PropertyNames = NuGet.Client.ManagedCodeConventions.PropertyNames;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class ValidatePackage : PackagingTask
    {
        [Required]
        public string ContractName
        {
            get;
            set;
        }

        [Required]
        public string[] ReferencePaths
        {
            get;
            set;
        }

        [Required]
        public string PackageId
        {
            get;
            set;
        }

        /// <summary>
        /// List of files in the package.
        ///   Identity: source path
        ///   TargetPath: path inside package
        ///   TargetFramework: target moniker of asset
        ///   Package: In the case that we are considering multiple packages
        ///            supporting the same contract, this will distinguish
        ///            files coming from other packages and in a different
        ///            resolution scope.
        ///            All pacakges will be considered when validating 
        ///            support and checking for binclashes.
        /// </summary>
        [Required]
        public ITaskItem[] Files
        {
            get;
            set;
        }

        /// <summary>
        /// Contracts supported by various frameworks.
        ///   Identity: ContractName
        ///   Version: API version
        ///   TargetFramework: Framework that supports the contract
        /// </summary>
        [Required]
        public ITaskItem[] ContractSupport
        {
            get;
            set;
        }

        /// <summary>
        /// Frameworks to evaluate.
        ///   Identity: Framework
        ///   RuntimeIDs: Semi-colon seperated list of runtime IDs
        /// </summary>
        [Required]
        public ITaskItem[] Frameworks
        {
            get;
            set;
        }

        /// <summary>
        /// Generations.json definition
        /// </summary>
        [Required]
        public string GenerationDefinitionsFile
        {
            get;
            set;
        }

        /// <summary>
        /// Path to runtime.json that contains the runtime graph.
        /// </summary>
        [Required]
        public string RuntimeFile
        {
            get;
            set;
        }

        public string SuppressionFile
        {
            get;
            set;
        }


        [Required]
        public string FrameworkListsPath
        {
            get;
            set;
        }

        public bool SkipGenerationCheck
        {
            get;
            set;
        }

        public bool SkipSupportCheck
        {
            get;
            set;
        }

        /// <summary>
        /// property bag of error suppressions
        /// </summary>
        private Dictionary<Suppression, HashSet<string>> _suppressions;
        private Dictionary<string, List<PackageItem>> _validateFiles;
        private Dictionary<NuGetFramework, ValidationFramework> _frameworks;
        private Generations _generations;
        private AggregateNuGetAssetResolver _resolver;
        private Dictionary<string, PackageItem> _targetPathToPackageItem;

        public override bool Execute()
        {
            LoadSuppressions();
            LoadFiles();
            LogPackageContent();
            LoadGenerations();
            LoadSupport();

            if (!SkipGenerationCheck)
            {
                ValidateGenerations();
            }

            if (!SkipSupportCheck)
            {
                // TODO: need to validate dependencies.
                ValidateSupport();
            }


            return !_log.HasLoggedErrors;
        }

        private void ValidateGenerations()
        {
            // get the generation of all portable implementation dlls.
            var allRuntimeGenerations = _resolver.GetAllRuntimeItems().Values
                .SelectMany(groups => groups.Select(group => group.Properties[PropertyNames.TargetFrameworkMoniker] as NuGetFramework))
                .Where(fx => fx != null && fx.Framework == FrameworkConstants.FrameworkIdentifiers.NetPlatform)
                .Select(fx => fx.Version);

            // get the generation of all supported frameworks (some may have framework specific implementations
            // or placeholders).
            var allSupportedGenerations = _frameworks.Values.Where(vf => vf.SupportedVersion != null && IsGeneration(vf.Framework))
                .Select(vf => vf.Framework.Version);

            // find the minimum supported version as the minimum of any generation explicitly implemented 
            // with a portable implementation, or the generation of a framework with a platform specific
            // implementation.
            Version minSupportedGeneration = allRuntimeGenerations.Concat(allSupportedGenerations).Min();

            // validate API version against generation for all files
            foreach (var validateFile in _validateFiles.SelectMany(packageFileSet => packageFileSet.Value)
                .Where(f => IsDll(f.SourcePath) && IsGeneration(f.TargetFramework)))
            {
                if (!File.Exists(validateFile.SourcePath))
                {
                    _log.LogError($"Cannot validate generation of {validateFile.SourcePath} because it does not exist");
                    continue;
                }

                var calculatedGeneration = _generations.DetermineGeneration(validateFile.SourcePath, _log);

                if (validateFile.TargetFramework.Version < calculatedGeneration)
                {
                    _log.LogError($"Invalid generation {validateFile.TargetFramework.Version} for {validateFile.SourcePath}, must be at least {calculatedGeneration} based on dependencies of the file and/or explicit seeding in {GenerationDefinitionsFile}.");
                }
                else if (validateFile.TargetFramework.Version > calculatedGeneration)
                {
                    _log.LogMessage(LogImportance.Low, $"Generation {validateFile.TargetFramework.Version} for {validateFile.SourcePath} is higher than the calculated miniumum {calculatedGeneration}.");
                }

                if (validateFile.TargetFramework.Version < minSupportedGeneration)
                {
                    _log.LogError($"Invalid generation {validateFile.TargetFramework.Version} for {validateFile.SourcePath}, must be at least {minSupportedGeneration} based on the implementations in the package.  If you meant to target the lower generation you may be missing an implementation for a framework on that lower generation.  If not you should raise the generation of the reference assembly to match that of the lowest supported generation of all implementations/placeholders.");
                }
            }
        }
        private void ValidateSupport()
        {
            // validate support for each TxM:RID
            foreach (var validateFramework in _frameworks.Values)
            {
                NuGetFramework fx = validateFramework.Framework;
                Version supportedVersion = validateFramework.SupportedVersion;

                var compileAssetPaths = _resolver.ResolveCompileAssets(fx, PackageId);
                bool hasCompileAsset, hasCompilePlaceHolder;
                ExamineAssets("Compile", ContractName, fx.ToString(), compileAssetPaths, out hasCompileAsset, out hasCompilePlaceHolder);

                // resolve/test for each RID associated with this framework.
                foreach (string runtimeId in validateFramework.RuntimeIds)
                {
                    string target = String.IsNullOrEmpty(runtimeId) ? fx.ToString() : $"{fx}/{runtimeId}";
                    var runtimeAssetPaths = _resolver.ResolveRuntimeAssets(fx, runtimeId);

                    bool hasRuntimeAsset, hasRuntimePlaceHolder;
                    ExamineAssets("Runtime", ContractName, target, runtimeAssetPaths, out hasRuntimeAsset, out hasRuntimePlaceHolder);

                    if (null == supportedVersion)
                    {
                        // Contract should not be supported on this platform.
                        bool permitImplementation = HasSuppression(Suppression.PermitImplementation, target);

                        if (hasCompileAsset && (hasRuntimeAsset & !permitImplementation))
                        {
                            _log.LogError($"{ContractName} should not be supported on {target} but has both compile and runtime assets.");
                        }
                        else if (hasRuntimeAsset & !permitImplementation)
                        {
                            _log.LogError($"{ContractName} should not be supported on {target} but has runtime assets.");
                        }

                        if (hasRuntimePlaceHolder && hasCompilePlaceHolder)
                        {
                            _log.LogError($"{ContractName} should not be supported on {target} but has placeholders for both compile and runtime which will permit the package to install.");
                        }
                    }
                    else
                    {
                        if (validateFramework.IsInbox)
                        {
                            if (!hasCompileAsset && !hasCompilePlaceHolder)
                            {
                                _log.LogError($"Framework {fx} should support {ContractName} inbox but was missing a placeholder for compile-time.  You may need to add <InboxOnTargetFramework Include=\"{fx.GetShortFolderName()}\" /> to your project.");
                            }
                            else if (hasCompileAsset)
                            {
                                _log.LogError($"Framework {fx} should support {ContractName} inbox but contained a reference assemblies: {String.Join(", ", compileAssetPaths)}.  You may need to add <InboxOnTargetFramework Include=\"{fx.GetShortFolderName()}\" /> to your project.");
                            }

                            if (!hasRuntimeAsset && !hasRuntimePlaceHolder)
                            {
                                _log.LogError($"Framework {fx} should support {ContractName} inbox but was missing a placeholder for run-time.  You may need to add <InboxOnTargetFramework Include=\"{fx.GetShortFolderName()}\" /> to your project.");
                            }
                            else if (hasRuntimeAsset)
                            {
                                _log.LogError($"Framework {fx} should support {ContractName} inbox but contained a implementation assemblies: {String.Join(", ", runtimeAssetPaths)}.  You may need to add <InboxOnTargetFramework Include=\"{fx.GetShortFolderName()}\" /> to your project.");
                            }
                        }
                        else
                        {
                            Version referenceAssemblyVersion = null;
                            if (!hasCompileAsset)
                            {
                                _log.LogError($"{ContractName} should be supported on {target} but has no compile assets.");
                            }
                            else
                            {
                                var referenceAssemblies = compileAssetPaths.Where(IsDll);

                                if (referenceAssemblies.Count() > 1)
                                {
                                    _log.LogError($"{ContractName} should only contain a single compile asset for {target}.");
                                }

                                foreach (var referenceAssembly in referenceAssemblies)
                                {
                                    referenceAssemblyVersion = _targetPathToPackageItem[referenceAssembly].Version;

                                    if (!VersionUtility.IsCompatibleApiVersion(supportedVersion, referenceAssemblyVersion))
                                    {
                                        _log.LogError($"{ContractName} should support API version {supportedVersion} on {target} but {referenceAssembly} was found to support {referenceAssemblyVersion?.ToString() ?? "<unknown version>"}.");
                                    }
                                }
                            }

                            if (!hasRuntimeAsset && !IsGeneration(validateFramework.Framework))
                            {
                                _log.LogError($"{ContractName} should be supported on {target} but has no runtime assets.");
                            }
                            else
                            {
                                var implementationAssemblies = runtimeAssetPaths.Where(IsDll);

                                Dictionary<string, string> implementationFiles = new Dictionary<string, string>();
                                foreach (var implementationAssembly in implementationAssemblies)
                                {
                                    Version implementationVersion = _targetPathToPackageItem[implementationAssembly].Version;

                                    if (!VersionUtility.IsCompatibleApiVersion(supportedVersion, implementationVersion))
                                    {
                                        _log.LogError($"{ContractName} should support API version {supportedVersion} on {target} but {implementationAssembly} was found to support {implementationVersion?.ToString() ?? "<unknown version>"}.");
                                    }

                                    if (referenceAssemblyVersion != null && implementationVersion != referenceAssemblyVersion)
                                    {
                                        _log.LogError($"{ContractName} has mismatched compile ({referenceAssemblyVersion}) and runtime ({implementationVersion}) versions on {target}.");
                                    }

                                    string fileName = Path.GetFileName(implementationAssembly);

                                    if (implementationFiles.ContainsKey(fileName))
                                    {
                                        _log.LogError($"{ContractName} includes both {implementationAssembly} and {implementationFiles[fileName]} an on {target} which have the same name and will clash when both packages are used.");
                                    }
                                    else
                                    {
                                        implementationFiles[fileName] = implementationAssembly;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool IsDll(string path)
        {
            return !String.IsNullOrWhiteSpace(path) && Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase);
        }

        private HashSet<string> GetSuppressionValues(Suppression key)
        {
            HashSet<string> values;
            _suppressions.TryGetValue(key, out values);
            return values;
        }

        private string GetSingleSuppressionValue(Suppression key)
        {
            var values = GetSuppressionValues(key);
            return (values != null && values.Count == 1) ? values.Single() : null;
        }


        private bool HasSuppression(Suppression key)
        {
            return _suppressions.ContainsKey(key);
        }

        private bool HasSuppression(Suppression key, string value)
        {
            HashSet<string> values;
            if (_suppressions.TryGetValue(key, out values) && values != null)
            {
                return values.Contains(value);
            }
            return false;
        }

        private void ExamineAssets(string assetType, string package, string target, IEnumerable<string> runtimeItems, out bool hasRealAsset, out bool hasPlaceHolder)
        {
            hasPlaceHolder = false;
            hasRealAsset = false;
            StringBuilder assetLog = new StringBuilder($"{assetType} assets for {ContractName} on {target}: ");
            if (runtimeItems != null && runtimeItems.Any())
            {
                foreach (var runtimeItem in runtimeItems)
                {
                    assetLog.AppendLine();
                    assetLog.Append($"  {runtimeItem}");

                    if (!hasRealAsset && NuGetAssetResolver.IsPlaceholder(runtimeItem))
                    {
                        hasPlaceHolder = true;
                    }
                    else
                    {
                        hasRealAsset = true;
                        hasPlaceHolder = false;
                    }
                }
            }
            else
            {
                assetLog.AppendLine();
                assetLog.Append("  <none>");
            }
            _log.LogMessage(LogImportance.Low, assetLog.ToString());
        }

        private void LoadSuppressions()
        {
            _suppressions = new Dictionary<Suppression, HashSet<string>>();
            if (File.Exists(SuppressionFile))
            {
                foreach (string suppression in File.ReadAllLines(SuppressionFile))
                {
                    if (suppression.TrimStart().StartsWith(@"//", StringComparison.OrdinalIgnoreCase) || String.IsNullOrWhiteSpace(suppression))
                    {
                        continue;
                    }

                    var parts = suppression.Split(new[] { '=' }, 2);
                    string keyString = null;
                    Suppression key;
                    string[] values = null;

                    if (parts.Length != 2)
                    {
                        // assume entire line is key
                        keyString = suppression;
                    }
                    else
                    {
                        keyString = parts[0];
                        values = parts[1].Split(';');
                    }

                    if (Enum.TryParse<Suppression>(keyString, out key))
                    {
                        _suppressions[key] = new HashSet<string>(values);
                    }
                    else
                    {
                        _log.LogError($"{SuppressionFile} contained unkown suppression {keyString}");
                    }
                }
            }
        }

        private void LoadFiles()
        {
            _validateFiles = new Dictionary<string, List<PackageItem>>();
            foreach (var file in Files)
            {
                try
                {
                    var validateFile = new PackageItem(file);

                    if (String.IsNullOrWhiteSpace(validateFile.TargetPath))
                    {
                        _log.LogError($"{validateFile.TargetPath} is missing TargetPath metadata");
                    }

                    if (IsDll(validateFile.SourcePath))
                    {
                        if (validateFile.TargetFramework == null)
                        {
                            _log.LogError($"{validateFile.SourcePath} is missing TargetFramework metadata");
                        }
                        else if (validateFile.TargetPath.IndexOf(validateFile.TargetFramework.GetShortFolderName(), StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            _log.LogError($"{validateFile.SourcePath} specifies TargetFramework {validateFile.TargetFramework} but TargetPath {validateFile.TargetPath} is missing the {validateFile.TargetFramework.GetShortFolderName()} qualifier");
                        }
                    }

                    if (!_validateFiles.ContainsKey(validateFile.Package))
                    {
                        _validateFiles[validateFile.Package] = new List<PackageItem>();
                    }
                    _validateFiles[validateFile.Package].Add(validateFile);
                }
                catch (Exception ex)
                {
                    _log.LogError($"Could not parse File {file.ItemSpec}. {ex}");
                    // skip it.
                }
            }

            // build a map to translate back to source file from resolved asset
            // we use package-specific paths since we're resolving a set of packages.
            _targetPathToPackageItem = new Dictionary<string, PackageItem>();
            foreach (var packageFiles in _validateFiles)
            {
                foreach (PackageItem validateFile in packageFiles.Value)
                {
                    string packageSpecificTargetPath = AggregateNuGetAssetResolver.AsPackageSpecificTargetPath(packageFiles.Key, validateFile.TargetPath);

                    if (_targetPathToPackageItem.ContainsKey(packageSpecificTargetPath))
                    {
                        _log.LogError($"Files {_targetPathToPackageItem[packageSpecificTargetPath].SourcePath} and {validateFile.SourcePath} have the same TargetPath {packageSpecificTargetPath}.");
                    }
                    _targetPathToPackageItem[packageSpecificTargetPath] = validateFile;
                }
            }

            _resolver = new AggregateNuGetAssetResolver(RuntimeFile);
            foreach (string packageId in _validateFiles.Keys)
            {
                _resolver.AddPackageItems(packageId, _validateFiles[packageId].Select(f => f.TargetPath));
            }
        }

        private void LoadGenerations()
        {
            _generations = Generations.Load(GenerationDefinitionsFile);

            _generations.ReferencePaths = ReferencePaths;
        }

        private void LogPackageContent()
        {
            foreach (var packageId in _validateFiles.Keys)
            {
                _log.LogMessage(LogImportance.Low, $"Package {packageId}");
                foreach (var targetPath in _validateFiles[packageId].Select(pi => pi.TargetPath))
                {
                    _log.LogMessage(LogImportance.Low, $"  {targetPath}");
                }
            }
        }

        private void LoadSupport()
        {
            _frameworks = new Dictionary<NuGetFramework, ValidationFramework>();

            // determine which TxM:RIDs should be considered for support based on Frameworks item
            foreach (var framework in Frameworks)
            {
                NuGetFramework fx;
                try
                {
                    fx = NuGetFramework.Parse(framework.ItemSpec);
                }
                catch (Exception ex)
                {
                    _log.LogError($"Could not parse Framework {framework.ItemSpec}. {ex}");
                    continue;
                }

                ValidationFramework validationFramework = null;
                if (!_frameworks.TryGetValue(fx, out validationFramework))
                {
                    _frameworks[fx] = validationFramework = new ValidationFramework(fx);
                }

                string runtimeIdList = framework.GetMetadata("RuntimeIDs");
                if (!String.IsNullOrWhiteSpace(runtimeIdList))
                {
                    validationFramework.RuntimeIds = runtimeIdList.Split(';');
                }
            }

            // determine what version should be supported based on ContractSupport items
            foreach (var supportedContract in ContractSupport.Where(c => c.ItemSpec == ContractName))
            {
                NuGetFramework fx;
                Version supportedVersion;
                string tfm = supportedContract.GetMetadata("TargetFramework");
                string version = supportedContract.GetMetadata("Version");

                try
                {
                    fx = NuGetFramework.Parse(tfm);
                }
                catch (Exception ex)
                {
                    _log.LogError($"Could not parse TargetFramework {tfm} on ContractSupport item {supportedContract.ItemSpec}. {ex}");
                    continue;
                }

                try
                {
                    supportedVersion = Version.Parse(version);
                }
                catch (Exception ex)
                {
                    _log.LogError($"Could not parse Version {version} on ContractSupport item {supportedContract.ItemSpec}. {ex}");
                    continue;
                }

                ValidationFramework validationFramework = null;
                if (_frameworks.TryGetValue(fx, out validationFramework))
                {
                    validationFramework.SupportedVersion = supportedVersion;
                }
                else
                {
                    _log.LogMessage(LogImportance.Low, $"Skipping validation for {fx} because it was not listed in Frameworks item list.");
                }
            }


            // determine which Frameworks should support inbox
            FrameworkSet inboxFrameworks = FrameworkSet.Load(FrameworkListsPath);
            foreach (IEnumerable<Framework> inboxFxGroup in inboxFrameworks.Frameworks.Values)
            {
                foreach (Framework inboxFx in inboxFxGroup)
                {
                    // get currently supported version to see if we have OOB'ed it
                    Version inboxVersion = null;
                    inboxFx.Assemblies.TryGetValue(ContractName, out inboxVersion);

                    if (inboxVersion != null)
                    {
                        NuGetFramework fx = NuGetFramework.Parse(inboxFx.ShortName);
                        ValidationFramework validationFramework = null;
                        if (_frameworks.TryGetValue(fx, out validationFramework))
                        {
                            Version supportedVersion = validationFramework.SupportedVersion;

                            if (supportedVersion != null &&
                                (supportedVersion.Major > inboxVersion.Major ||
                                (supportedVersion.Major == inboxVersion.Major && supportedVersion.Minor > inboxVersion.Minor)))
                            {
                                // Higher major.minor
                                _log.LogMessage(LogImportance.Low, $"Framework {fx} supported {ContractName} as inbox but the current supported version {supportedVersion} is higher in major.minor than inbox version {inboxVersion}.  Assuming out of box.");
                                continue;
                            }
                            else if (supportedVersion != null && supportedVersion < inboxVersion)
                            {
                                // Lower version
                                _log.LogError($"Framework {fx} supports {ContractName} as inbox but the current supported version {supportedVersion} is lower than the inbox version {inboxVersion}");
                            }

                            // equal major.minor, build.revision difference is permitted, prefer the version listed by ContractSupport item
                        }

                        if (validationFramework == null)
                        {
                            // we may not be explicitly validating for this framework so add it to validate inbox assets.
                            _frameworks[fx] = validationFramework = new ValidationFramework(fx)
                            {
                                SupportedVersion = inboxVersion
                            };
                        }

                        validationFramework.IsInbox = true;
                    }
                }
            }

            // for every framework we know about, also validate it's generation to ensure it can
            // be targeted by PCL.  Even if a package only supports a single framework we still
            // want to include a portable reference assembly.  This allows 3rd parties to add 
            // their own implementation via a lineup/runtime.json.

            // only consider frameworks that support the contract at a specific version
            Version maxVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
            var portableFrameworks = _frameworks.Values.Where(fx => fx.SupportedVersion != null && fx.SupportedVersion != maxVersion).ToArray();

            var genVersionSuppression = GetSuppressionValues(Suppression.PermitPortableVersionMismatch) ?? new HashSet<string>();
            Dictionary<NuGetFramework, ValidationFramework> generationsToValidate = new Dictionary<NuGetFramework, ValidationFramework>();
            foreach (var framework in portableFrameworks)
            {
                NuGetFramework generation = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetPlatform, _generations.DetermineGeneration(framework.Framework));
                _log.LogMessage(LogImportance.Low, $"Validating {generation} for {ContractName}, {framework.SupportedVersion} since it is supported by {framework.Framework}");

                ValidationFramework existingGeneration = null;
                if (generationsToValidate.TryGetValue(generation, out existingGeneration))
                {
                    if ((existingGeneration.SupportedVersion > framework.SupportedVersion) && !genVersionSuppression.Contains(framework.Framework.ToString()))
                    {
                        _log.LogError($"Framework {framework.Framework} supports {ContractName} at {framework.SupportedVersion} which is lower than {existingGeneration.SupportedVersion} supported by generation {generation.GetShortFolderName()}");
                    }
                }
                else
                {
                    generationsToValidate.Add(generation, new ValidationFramework(generation) { SupportedVersion = framework.SupportedVersion });
                }
            }

            foreach (var generation in generationsToValidate)
            {
                _frameworks.Add(generation.Key, generation.Value);
            }

            // for every generation supported explcitly in implementation, ensure
            // it can be targeted by PCL.
            foreach (var packageGroup in _resolver.GetAllRuntimeItems())
            {
                var allGenerationalImplementations = packageGroup.Value
                    .Where(contentGroup => IsGeneration(contentGroup.Properties[PropertyNames.TargetFrameworkMoniker] as NuGetFramework))
                    .SelectMany(contentGroup => contentGroup.Items.Select(item => _targetPathToPackageItem[AggregateNuGetAssetResolver.AsPackageSpecificTargetPath(packageGroup.Key, item.Path)]));

                foreach (var generationalImplementation in allGenerationalImplementations)
                {
                    NuGetFramework generation = generationalImplementation.TargetFramework;
                    if (_frameworks.ContainsKey(generation))
                    {
                        continue;
                    }

                    Version supportedVersion = generationalImplementation.Version;

                    _log.LogMessage(LogImportance.Low, $"Validating {generation} for {ContractName}, {supportedVersion} since it is supported by {generationalImplementation.TargetPath}");

                    _frameworks.Add(generation, new ValidationFramework(generation) { SupportedVersion = supportedVersion });
                }
            }
        }

        private static bool IsGeneration(NuGetFramework framework)
        {
            return framework != null && framework.Framework == FrameworkConstants.FrameworkIdentifiers.NetPlatform;
        }


        private class ValidationFramework
        {
            private static readonly string[] s_nullRidList = new string[] { null };
            public ValidationFramework(NuGetFramework framework)
            {
                Framework = framework;
                RuntimeIds = s_nullRidList;
            }

            public NuGetFramework Framework { get; }
            public string[] RuntimeIds { get; set; }

            // if null indicates the contract should not be supported.
            public Version SupportedVersion { get; set; }
            public bool IsInbox { get; set; }
        }
    }
    public enum Suppression
    {
        /// <summary>
        /// Permits a runtime asset of the targets specified, semicolon delimited
        /// </summary>
        PermitImplementation,
        /// <summary>
        /// Permits a lower version on specified frameworks, semicolon delimitied, than the generation supported by that framework
        /// </summary>
        PermitPortableVersionMismatch
    }
}
