// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GeneratePackageReport : PackagingTask
    {
        private Dictionary<string, PackageItem> _targetPathToPackageItem;
        private AggregateNuGetAssetResolver _resolver;
        private Dictionary<NuGetFramework, string[]> _frameworks;
        private NuGetAssetResolver _resolverWithoutPlaceholders;

        [Required]
        public string PackageId
        {
            get;
            set;
        }

        [Required]
        public string PackageVersion
        {
            get;
            set;
        }

        [Required]
        public ITaskItem[] Files
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
        /// Path to runtime.json that contains the runtime graph.
        /// </summary>
        [Required]
        public string RuntimeFile { get; set; }

        [Required]
        public string FrameworkListsPath
        {
            get;
            set;
        }

        /// <summary>
        /// JSON file describing results of validation
        /// </summary>
        [Required]
        public string ValidationReport
        {
            get;
            set;
        }

        public override bool Execute()
        {
            LoadFiles();
            LoadFrameworks();

            var report = new ValidationReport()
            {
                Id = PackageId,
                Version = PackageVersion,
                SupportedFrameworks = new Dictionary<string, string>()
            };

            string package = $"{PackageId}/{PackageVersion}";

            foreach (var framework in _frameworks.OrderBy(f => f.Key.ToString()))
            {
                var fx = framework.Key;
                var runtimeIds = framework.Value;

                var compileAssets = _resolver.ResolveCompileAssets(fx, PackageId);

                bool hasCompileAsset, hasCompilePlaceHolder;
                NuGetAssetResolver.ExamineAssets(Log, "Compile", package, fx.ToString(), compileAssets, out hasCompileAsset, out hasCompilePlaceHolder);

                // start by making sure it has some asset available for compile
                var isSupported = hasCompileAsset || hasCompilePlaceHolder;

                if (runtimeIds.All(rid => !String.IsNullOrEmpty(rid)))
                {
                    // Add Framework only (compile) target if all RIDs are non-empty.
                    // This acts as a compile target for a framework that requires a RID for runtime.
                    var reportTarget = new Target()
                    {
                        Framework = fx.ToString(),
                        RuntimeID = null,
                        CompileAssets = compileAssets.Select(c => GetPackageAssetFromTargetPath(c)).ToArray()
                    };
                    report.Targets.Add(fx.ToString(), reportTarget);
                }

                foreach (var runtimeId in runtimeIds)
                {
                    string target = String.IsNullOrEmpty(runtimeId) ? fx.ToString() : $"{fx}/{runtimeId}";

                    var runtimeAssets = _resolver.ResolveRuntimeAssets(fx, runtimeId);

                    bool hasRuntimeAsset, hasRuntimePlaceHolder;
                    NuGetAssetResolver.ExamineAssets(Log, "Runtime", package, target, runtimeAssets, out hasRuntimeAsset, out hasRuntimePlaceHolder);

                    if (!FrameworkUtilities.IsGenerationMoniker(fx) && !fx.IsPCL)
                    {
                        // only look at runtime assets for runnable frameworks.
                        isSupported &= hasCompileAsset == hasRuntimeAsset;
                        isSupported &= hasCompilePlaceHolder == hasRuntimePlaceHolder;
                    }

                    var reportTarget = new Target()
                    {
                        Framework = fx.ToString(),
                        RuntimeID = runtimeId,
                        CompileAssets = compileAssets.Select(c => GetPackageAssetFromTargetPath(c)).ToArray(),
                        RuntimeAssets = runtimeAssets.Select(r => GetPackageAssetFromTargetPath(r)).ToArray()
                    };
                    report.Targets[target] = reportTarget;
                }
                
                if (isSupported)
                {
                    // Find version
                    // first try the resolved compile asset for this package
                    var refAssm = compileAssets.FirstOrDefault(r => !NuGetAssetResolver.IsPlaceholder(r))?.Substring(PackageId.Length + 1);

                    if (refAssm == null)
                    {
                        // if we didn't have a compile asset it means this framework is supported inbox with a placeholder
                        // resolve the assets without placeholders to pick up the netstandard reference assembly.
                        compileAssets = _resolverWithoutPlaceholders.ResolveCompileAssets(fx);
                        refAssm = compileAssets.FirstOrDefault(r => !NuGetAssetResolver.IsPlaceholder(r));
                    }

                    var version = "unknown";
                    if (refAssm != null)
                    {
                        version = _targetPathToPackageItem[AggregateNuGetAssetResolver.AsPackageSpecificTargetPath(PackageId, refAssm)].Version?.ToString() ?? version;
                    }

                    report.SupportedFrameworks.Add(fx.ToString(), version);
                }
            }

            report.Save(ValidationReport);

            return !Log.HasLoggedErrors;
        }

        private static string[] s_noRids = new[] { string.Empty };
        private void LoadFrameworks()
        {
            _frameworks = new Dictionary<NuGetFramework, string[]>(NuGetFramework.Comparer);

            // load the specified frameworks
            foreach(var framework in Frameworks)
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

                _frameworks.Add(fx, runtimeIds);
            }

            // validate for any TFMs explicitly targeted
            var fileFrameworks = _targetPathToPackageItem.Values.Select(f => f.TargetFramework).Distinct(NuGetFramework.Comparer).Where(f => f != null);
            foreach(var fileFramework in fileFrameworks)
            {
                if (!_frameworks.ContainsKey(fileFramework))
                {
                    _frameworks.Add(fileFramework, s_noRids);
                }
            }

            // validate for TFMs inbox
            var frameworkData = FrameworkSet.Load(FrameworkListsPath);
            var inboxFrameworks = frameworkData.Frameworks.SelectMany(f => f.Value)
                .Where(fx => fx.Assemblies.ContainsKey(PackageId))
                .Select(fx => NuGetFramework.Parse(fx.FrameworkName.FullName));
            
            foreach (var inboxFramework in inboxFrameworks)
            {
                if (!_frameworks.ContainsKey(inboxFramework))
                {
                    _frameworks.Add(inboxFramework, s_noRids);
                }
            }

            // validate for derived TFMs
            var expander = new FrameworkExpander();
            foreach(var framework in _frameworks.Keys.ToArray())
            {
                var derivedFxs = expander.Expand(framework);
                //.Where(fx => fx.Framework == FrameworkConstants.FrameworkIdentifiers.NetStandard)
                foreach (var derivedFx in derivedFxs)
                {
                    if (!_frameworks.ContainsKey(derivedFx))
                    {
                        _frameworks.Add(derivedFx, s_noRids);
                    }
                }
            }
        }

        private void LoadFiles()
        {
            var packageItems = new Dictionary<string, List<PackageItem>>();
            foreach (var file in Files)
            {
                try
                {
                    var packageItem = new PackageItem(file);

                    if (!packageItem.IsDll && !packageItem.IsPlaceholder)
                    {
                        continue;
                    }

                    if (String.IsNullOrWhiteSpace(packageItem.TargetPath))
                    {
                        Log.LogError($"{packageItem.TargetPath} is missing TargetPath metadata");
                    }

                    string packageId = packageItem.Package ?? PackageId;

                    if (!packageItems.ContainsKey(packageId))
                    {
                        packageItems[packageId] = new List<PackageItem>();
                    }
                    packageItems[packageId].Add(packageItem);
                }
                catch (Exception ex)
                {
                    Log.LogError($"Could not parse File {file.ItemSpec}. {ex}");
                    // skip it.
                }
            }

            // build a map to translate back to source file from resolved asset
            // we use package-specific paths since we're resolving a set of packages.
            _targetPathToPackageItem = new Dictionary<string, PackageItem>();
            foreach (var packageFiles in packageItems)
            {
                foreach (PackageItem validateFile in packageFiles.Value)
                {
                    string packageSpecificTargetPath = AggregateNuGetAssetResolver.AsPackageSpecificTargetPath(packageFiles.Key, validateFile.TargetPath);

                    if (_targetPathToPackageItem.ContainsKey(packageSpecificTargetPath))
                    {
                        Log.LogError($"Files {_targetPathToPackageItem[packageSpecificTargetPath].SourcePath} and {validateFile.SourcePath} have the same TargetPath {packageSpecificTargetPath}.");
                    }
                    _targetPathToPackageItem[packageSpecificTargetPath] = validateFile;
                }
            }

            _resolver = new AggregateNuGetAssetResolver(RuntimeFile);
            foreach (string packageId in packageItems.Keys)
            {
                _resolver.AddPackageItems(packageId, packageItems[packageId].Select(f => f.TargetPath));
            }

            // create a resolver that can be used to determine the API version for inbox assemblies
            // since inbox assemblies are represented with placeholders we can remove the placeholders
            // and use the netstandard reference assembly to determine the API version
            if (packageItems.Any() && packageItems.ContainsKey(PackageId))
            {
                var filesWithoutPlaceholders = packageItems[PackageId]
                    .Select(pf => pf.TargetPath)
                    .Where(f => !NuGetAssetResolver.IsPlaceholder(f));

                _resolverWithoutPlaceholders = new NuGetAssetResolver(RuntimeFile, filesWithoutPlaceholders);
            }
        }

        private PackageAsset GetPackageAssetFromTargetPath(string targetPath)
        {
            PackageItem packageItem = null;
            if (!_targetPathToPackageItem.TryGetValue(targetPath, out packageItem))
            {
                throw new ArgumentException($"Could not find source item for {targetPath}", nameof(targetPath));
            }

            var packageAsset = new PackageAsset()
            {
                HarvestedFrom = packageItem.HarvestedFrom,
                LocalPath = packageItem.SourcePath,
                PackagePath = packageItem.TargetPath,
                TargetFramework = packageItem.TargetFramework,
                Version = packageItem.Version
            };

            if (packageItem.SourceProject != null)
            {
                packageAsset.SourceProject = new BuildProject()
                {
                    Project = packageItem.SourceProject,
                    AdditionalProperties = packageItem.AdditionalProperties,
                    UndefineProperties = packageItem.UndefineProperties
                };
            }

            return packageAsset;
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

            public string ShortName { get { return Framework.GetShortFolderName(); } }            
        }
    }
}
