// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Microsoft.NuGet.Build.Tasks
{
    /// <summary>
    /// Resolves the assets out of packages in the project.json.
    /// </summary>
    public sealed class ResolveNuGetPackageAssets : Task
    {
        private readonly List<ITaskItem> _analyzers = new List<ITaskItem>();
        private readonly List<ITaskItem> _copyLocalItems = new List<ITaskItem>();
        private readonly List<ITaskItem> _references = new List<ITaskItem>();

        /// <summary>
        /// Creates a new <see cref="ResolveNuGetPackageAssets"/>.
        /// </summary>
        public ResolveNuGetPackageAssets()
        { }

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

        public string ProjectLockFile
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
            JObject lockFile;
            using (var streamReader = new StreamReader(ProjectLockFile))
            {
                lockFile = JObject.Load(new JsonTextReader(streamReader));
            }

            var targets = (JObject)lockFile["targets"];

            string tfm = TargetFrameworkMonikers.First().Replace(" ", "");
            string rid = "win7";

            if (!String.IsNullOrEmpty(Architecture))
                rid += "-" + Architecture;

            if (UseDotNetNativeToolchain)
            {
                rid += "-aot";
            }

            var target = (JObject)targets[tfm + "/" + rid];

            if (target == null)
            {
                // we don't yet have proper portable support, so fake it for now.
                target = (JObject)targets[tfm];
            }

            if (target == null)
            {
                Log.LogError("Couldn't find the required information in the lock file. Make sure you have {0} in your frameworks list and {1} in your runtimes list.", tfm, rid);
                return false;
            }

            foreach (var package in target)
            {
                var packageNameParts = package.Key.Split('/');
                var packageName = packageNameParts[0];
                var packageVersion = packageNameParts[1];

                Log.LogMessage(MessageImportance.Low, "Resolved references from {0}:", packageName);

                foreach (var referenceItem in CreateItems(packageName, packageVersion, package.Value, "compile"))
                {
                    Log.LogMessage(MessageImportance.Low, "    Build time reference: {0}", referenceItem.ItemSpec);
                    _references.Add(referenceItem);
                }

                foreach (var copyLocalItem in CreateItems(packageName, packageVersion, package.Value, "runtime"))
                {
                    Log.LogMessage(MessageImportance.Low, "    Copy local reference: {0}", copyLocalItem.ItemSpec);
                    _copyLocalItems.Add(copyLocalItem);
                }

                foreach (var copyLocalItem in CreateItems(packageName, packageVersion, package.Value, "native"))
                {
                    Log.LogMessage(MessageImportance.Low, "    Copy local reference: {0}", copyLocalItem.ItemSpec);
                    _copyLocalItems.Add(copyLocalItem);
                }
            }

            return true;
        }

        private IEnumerable<ITaskItem> CreateItems(string packageName, string packageVersion, JToken packageObject, string key)
        {
            List<string> values = new List<string>();

            JToken tokenValue = packageObject[key];
            if (tokenValue != null)
            {
                switch (tokenValue.Type)
                {
                    case JTokenType.Array:
                        values.AddRange(((JArray)tokenValue).Values<string>());
                        break;
                    case JTokenType.Object:
                        values.AddRange(((JObject)tokenValue).Properties().Select(p => p.Name));
                        break;
                    default:
                        throw new InvalidOperationException(String.Format("Unexpected JToken type {0}.", tokenValue.Type));
                }
            }

            List<ITaskItem> items = new List<ITaskItem>();
            string packagesFolder = String.IsNullOrEmpty(PackageRoot) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dnx", "packages") : PackageRoot;
            string dnxPackage = Path.Combine(packagesFolder, packageName, packageVersion);

            foreach (string value in values)
            {
                var item = new TaskItem(Path.Combine(dnxPackage, value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)));

                item.SetMetadata("NuGetPackageName", packageName);
                item.SetMetadata("NuGetPackageVersion", packageVersion);

                // The ReferenceGrouping version expects numeric-dotted versions only
                var referenceGroupingPackageVersion = packageVersion.Split('-').First();
                item.SetMetadata("ReferenceGrouping", packageName + ",Version=" + referenceGroupingPackageVersion);
                item.SetMetadata("ReferenceGroupingDisplayName", packageName + " (Package)");
                item.SetMetadata("Private", "false");

                items.Add(item);
            }

            if (key == "runtime")
            {
                // workaround https://github.com/aspnet/dnx/issues/1782
                // dnx isn't including exe's in restore calculations
                // include any exe's next to active assets, otherwise include all EXEs
                string exeSearchPath = dnxPackage;
                var firstItem = items.FirstOrDefault();
                if (firstItem != null)
                {
                    exeSearchPath = Path.GetDirectoryName(firstItem.ItemSpec);
                }

                foreach (string exe in Directory.GetFiles(exeSearchPath, "*.exe", SearchOption.AllDirectories))
                {
                    var item = new TaskItem(Path.Combine(dnxPackage, exe.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)));

                    item.SetMetadata("NuGetPackageName", packageName);
                    item.SetMetadata("NuGetPackageVersion", packageVersion);

                    // The ReferenceGrouping version expects numeric-dotted versions only
                    var referenceGroupingPackageVersion = packageVersion.Split('-').First();
                    item.SetMetadata("ReferenceGrouping", packageName + ",Version=" + referenceGroupingPackageVersion);
                    item.SetMetadata("ReferenceGroupingDisplayName", packageName + " (Package)");
                    item.SetMetadata("Private", "false");

                    items.Add(item);
                }
            }

            return items;
        }
    }
}
