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
    public class GetLayoutFiles : PackagingTask
    {
        /// <summary>
        /// Package report files
        /// </summary>
        [Required]
        public string[] PackageReports { get; set; }

        /// <summary>
        /// Destination directory for layout
        /// </summary>
        [Required]
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// Optional set of frameworks to restrict the layout
        ///   Identity: Framework
        ///   RuntimeIDs: Semi-colon seperated list of runtime IDs
        /// </summary>
        public ITaskItem[] Frameworks { get; set; }

        /// <summary>
        /// Files to be copied to layout
        ///   Identity : source path
        ///   Destination : destination path
        /// </summary>
        [Output]
        public ITaskItem[] LayoutFiles { get; set; }
        
        public override bool Execute()
        {
            var frameworks = Frameworks.NullAsEmpty().ToDictionary(
                i => NuGetFramework.Parse(i.ItemSpec),
                i => 
                {
                    var rids = i.GetMetadata("RuntimeIds");
                    return String.IsNullOrEmpty(rids) ? new HashSet<string>() : new HashSet<string>(rids.Split(';'));
                },
                NuGetFramework.Comparer);

            var layoutFiles = new List<ITaskItem>();

            foreach(var packageReportFile in PackageReports)
            {
                var packageReport = PackageReport.Load(packageReportFile);

                foreach(var targetInfo in packageReport.Targets)
                {
                    var targetName = targetInfo.Key;
                    var target = targetInfo.Value;

                    var targetParts = targetName.Split('/');

                    var fx = NuGetFramework.Parse(targetParts[0]);
                    string rid = null;

                    if (targetParts.Length > 1)
                    {
                        rid = targetParts[1];
                    }
                    
                    if (frameworks.Count != 0)
                    {
                        HashSet<string> rids = null;

                        if (!frameworks.TryGetValue(fx, out rids))
                        {
                            Log.LogMessage(LogImportance.Low, $"Skipping {fx} since it is not in {nameof(Frameworks)}");
                            continue;
                        }

                        if (rid != null && rids.Count > 0 && !rids.Contains(rid))
                        {
                            Log.LogMessage(LogImportance.Low, $"Skipping {fx}/{rid} since it is not in {nameof(Frameworks)}");
                            continue;
                        }
                    }

                    if (!packageReport.SupportedFrameworks.ContainsKey(fx.ToString()))
                    {
                        Log.LogMessage(LogImportance.Low, $"Skipping {fx} since it is not supported");
                        continue;
                    }

                    if (rid == null)
                    {
                        // only consider compile assets for RID-less target
                        layoutFiles.AddRange(CreateLayoutFiles(target.CompileAssets, "ref", fx, null, "Compile"));
                    }

                    layoutFiles.AddRange(CreateLayoutFiles(target.RuntimeAssets, "runtime", fx, rid, "Runtime"));
                    layoutFiles.AddRange(CreateLayoutFiles(target.NativeAssets, "runtime", fx, rid, "Native"));
                }
            }

            LayoutFiles = layoutFiles.ToArray();

            return !Log.HasLoggedErrors;
        }


        private IEnumerable<ITaskItem> CreateLayoutFiles(IEnumerable<PackageAsset> assets, string subFolder, NuGetFramework framework, string rid, string assetType)
        {
            return assets.Where(a => !NuGetAssetResolver.IsPlaceholder(a.LocalPath))
                .Select(a => CreateLayoutFile(a.LocalPath, subFolder, framework, rid, assetType));
                 
        }

        private ITaskItem CreateLayoutFile(string source, string subfolder, NuGetFramework framework,  string rid, string assetType)
        {
            var item = new TaskItem(source);

            string fx = framework.GetShortFolderName();
            var destination = Path.Combine(DestinationDirectory, subfolder, fx);

            if (rid != null)
            {
                destination = Path.Combine(destination, rid);
            }

            destination = Path.Combine(destination, Path.GetFileName(source));

            item.SetMetadata("Destination", destination);
            item.SetMetadata("Framework", fx);

            if (!String.IsNullOrEmpty(rid))
            {
                item.SetMetadata("RuntimeID", rid);
            }

            item.SetMetadata("AssetType", assetType);

            return item;
        }
    }
}
