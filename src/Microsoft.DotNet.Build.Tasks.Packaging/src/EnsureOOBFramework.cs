// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.ContentModel;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// Examines assets to ensure that an OOB framework still has out-of-box
    /// assets if a previous version of that framework had inbox assets
    /// </summary>
    public class EnsureOOBFramework : PackagingTask
    {
        [Required]
        public string[] OOBFrameworks { get; set; }

        [Required]
        public ITaskItem[] Files { get; set; }

        public string RuntimeJson { get; set; }

        public string RuntimeId { get; set; }

        [Output]
        public ITaskItem[] AdditionalFiles { get; set; }


        private IEnumerable<PackageItem> _packageItems;

        public override bool Execute()
        {
            _packageItems = Files.Select(f => new PackageItem(f));
            var packageDlls = _packageItems.Where(pi => Path.GetExtension(pi.SourcePath).Equals(".dll", StringComparison.OrdinalIgnoreCase));
            var packagePaths = _packageItems.Select(pi => pi.TargetPath);
            var packagePathsWithoutPlaceHolders = packagePaths.Where(pi => !NuGetAssetResolver.IsPlaceholder(pi));

            if (!String.IsNullOrEmpty(RuntimeJson) && !File.Exists(RuntimeJson))
            {
                Log.LogError("Could not load runtime file: {0}", RuntimeJson);
                RuntimeJson = null;
            }

            NuGetAssetResolver resolver = new NuGetAssetResolver(RuntimeJson, packagePaths);
            NuGetAssetResolver obscuredResolver = new NuGetAssetResolver(RuntimeJson, packagePathsWithoutPlaceHolders);

            List<ITaskItem> newItems = new List<ITaskItem>();

            // determine if an inbox placeholder obscures an OOB implementation.
            foreach (var oobFx in OOBFrameworks)
            {
                NuGetFramework targetFramework = NuGetFramework.Parse(oobFx);

                // first see if any dlls are explicitly marked for this framework.
                IEnumerable<string> obscuredCompileFolders = packageDlls.Where(pi => pi.OriginalItem.GetMetadata("EnsureOOBFrameworkRef") == oobFx).Select(pi => pi.TargetDirectory);

                if (!obscuredCompileFolders.Any())
                {
                    // no dlls were marked, resolve without placeholders to determine what to promote.
                    var compileItems = resolver.GetCompileItems(targetFramework);
                    var obscuredCompileItems = obscuredResolver.GetCompileItems(targetFramework);
                    obscuredCompileFolders = GetObscuredAssetFolders(compileItems, obscuredCompileItems, targetFramework, targetFrameworkName: oobFx, expectedAssetFolder: "ref", ignoredAssetFolder: "lib");
                }

                var promotedCompileItems = ExpandAssetFoldersToItems(obscuredCompileFolders, targetAssetFolder: "ref", targetFrameworkName: oobFx);
                newItems.AddRange(promotedCompileItems);

                // don't use 'any' in paths due to https://github.com/NuGet/Home/issues/1676
                string targetLibFolder = !String.IsNullOrEmpty(RuntimeId) && RuntimeId != "any" ? $"runtimes/{RuntimeId}/lib" : "lib";
                IEnumerable<string> obscuredRuntimeFolders = packageDlls.Where(pi => pi.OriginalItem.GetMetadata("EnsureOOBFrameworkLib") == oobFx).Select(pi => pi.TargetDirectory);

                if (!obscuredRuntimeFolders.Any())
                {
                    var runtimeItems = resolver.GetRuntimeItems(targetFramework, RuntimeId);
                    var obscuredRuntimeItems = obscuredResolver.GetRuntimeItems(targetFramework, RuntimeId);
                    obscuredRuntimeFolders = GetObscuredAssetFolders(runtimeItems, obscuredRuntimeItems, targetFramework, targetFrameworkName: oobFx, expectedAssetFolder: targetLibFolder);
                }

                var promotedRuntimeItems = ExpandAssetFoldersToItems(obscuredRuntimeFolders, targetLibFolder, targetFrameworkName: oobFx);
                newItems.AddRange(promotedRuntimeItems);
            }

            AdditionalFiles = newItems.ToArray();

            return !Log.HasLoggedErrors;
        }

        private IEnumerable<string> GetObscuredAssetFolders(ContentItemGroup assets, ContentItemGroup obscuredAssets, NuGetFramework targetFramework, string targetFrameworkName, string expectedAssetFolder, string ignoredAssetFolder = null)
        {
            if (assets == null || assets.Items.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            if (assets.Items.Any(ci => !NuGetAssetResolver.IsPlaceholder(ci.Path)))
            {
                return Enumerable.Empty<string>();
            }

            if (targetFrameworkName == null)
            {
                targetFrameworkName = targetFramework.GetShortFolderName();
            }

            var resolvedFramework = assets.Properties["tfm"] as NuGetFramework;
            if (targetFramework.Equals(resolvedFramework))
            {
                Log.LogMessage(LogImportance.Low, $"Not overriding explicit placeholder for {targetFrameworkName}");
                return Enumerable.Empty<string>();
            }

            var obscuredAssetPaths = NuGetAssetResolver.GetPackageTargetDirectories(obscuredAssets);

            if (ignoredAssetFolder != null)
            {
                string ignoredFolder = ignoredAssetFolder + '/';
                obscuredAssetPaths = obscuredAssetPaths.Where(i => -1 == i.IndexOf(ignoredFolder, StringComparison.OrdinalIgnoreCase));
            }

            if (expectedAssetFolder != null)
            {
                var unexpectedAssetPaths = obscuredAssetPaths.Where(ri => !ri.StartsWith(expectedAssetFolder, StringComparison.OrdinalIgnoreCase));
                foreach (var unexpectedAssetPath in unexpectedAssetPaths)
                {
                    Log.LogWarning($"Unexpected targetPath {unexpectedAssetPath}.  Expected only {expectedAssetFolder}.");
                }

                // filter after we've warned
                obscuredAssetPaths = obscuredAssetPaths.Except(unexpectedAssetPaths);
            }

            if (!obscuredAssetPaths.Any())
            {
                // it's acceptable to have no override, this is the case for packages which 
                // carry implementation in a runtime-specific package
                Log.LogMessage(LogImportance.Low, $"No {expectedAssetFolder} assets could be found to override inbox placeholder for {targetFrameworkName}.");
            }

            return obscuredAssetPaths;
        }

        private IEnumerable<ITaskItem> ExpandAssetFoldersToItems(IEnumerable<string> keyAssets, string targetAssetFolder, string targetFrameworkName)
        {
            // Asset selection only finds dlls, but we need everything under the path.
            foreach (var packageItem in _packageItems)
            {
                foreach (var keyAsset in keyAssets)
                {
                    if (packageItem.TargetPath.StartsWith(keyAsset))
                    {
                        string subPath = packageItem.TargetPath.Substring(keyAsset.Length);
                        Log.LogMessage(LogImportance.Low, $"Copying {packageItem.TargetPath} to {targetAssetFolder}/{targetFrameworkName}{subPath}.");
                        yield return GetOOBItem(packageItem, $"{targetAssetFolder}/{targetFrameworkName}{subPath}", targetFrameworkName);
                    }
                }
            }
        }


        private static ITaskItem GetOOBItem(PackageItem oobItem, string targetPath, string targetFramework)
        {
            TaskItem item = new TaskItem(oobItem.OriginalItem);
            item.SetMetadata("TargetPath", targetPath);
            item.SetMetadata("TargetFramework", targetFramework);
            return item;
        }
    }
}
