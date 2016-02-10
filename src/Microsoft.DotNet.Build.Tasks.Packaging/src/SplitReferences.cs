// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class SplitReferences : PackagingTask
    {
        [Required]
        public ITaskItem[] References
        {
            get;
            set;
        }

        public string TargetFramework
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

        [Output]
        public ITaskItem[] PackageReferences
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] FrameworkReferences
        {
            get;
            set;
        }

        public override bool Execute()
        {
            if (References == null || References.Length == 0)
                return true;
            
            Dictionary<string, ITaskItem> packageReferences = new Dictionary<string, ITaskItem>();
            Dictionary<string, ITaskItem> assemblyReferences = new Dictionary<string, ITaskItem>();

            bool referencesMscorlib = false;

            NuGetFramework targetFx = NuGetFramework.Parse(TargetFramework);
            bool isOobFramework = targetFx.Equals(FrameworkConstants.CommonFrameworks.NetCore50) || targetFx.Framework == FrameworkConstants.FrameworkIdentifiers.UAP;

            foreach (var reference in References)
            {
                string referenceName = reference.ItemSpec;
                referencesMscorlib |= referenceName.Equals("mscorlib");
                string referenceVersion = reference.GetMetadata("Version");
                reference.SetMetadata("TargetFramework", TargetFramework);
                if (!string.IsNullOrEmpty(TargetFramework) && !isOobFramework && Frameworks.IsInbox(FrameworkListsPath, TargetFramework, referenceName, referenceVersion))
                {
                    AddReference(assemblyReferences, reference);
                }
                else
                {
                    AddReference(packageReferences, reference);
                }
            }

            if (referencesMscorlib)
            {
                // only add framework references for mscorlib-based assemblies.
                FrameworkReferences = assemblyReferences.Values.ToArray();
            }

            if (packageReferences.Count == 0)
            {
                var emptyItem = new TaskItem("_._");
                emptyItem.SetMetadata("TargetFramework", TargetFramework);
                packageReferences.Add(emptyItem.ItemSpec, emptyItem);
            }

            PackageReferences = packageReferences.Values.ToArray();


            return true;
        }

        private static void AddReference(Dictionary<string, ITaskItem> collection, ITaskItem item)
        {
            ITaskItem existingItem;

            if (collection.TryGetValue(item.ItemSpec, out existingItem))
            {
                Version existingVersion = Version.Parse(existingItem.GetMetadata("Version"));
                Version newVersion = Version.Parse(existingItem.GetMetadata("Version"));

                if (newVersion > existingVersion)
                {
                    collection[item.ItemSpec] = item;
                }
            }
            else
            {
                collection[item.ItemSpec] = item;
            }
        }
    }
}
