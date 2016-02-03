// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// Raises dependencies to a baseline version
    /// </summary>
    public class ApplyBaseLine : PackagingTask
    {
        [Required]
        public ITaskItem[] OriginalDependencies { get; set; }

        [Required]
        public ITaskItem[] BaseLinePackages { get; set; }
        
        [Output]
        public ITaskItem[] BaseLinedDependencies { get; set; }
        
        public override bool Execute()
        {
            Dictionary<string, string> baseLineVersions = new Dictionary<string, string>();
            foreach(var baseLinePackage in BaseLinePackages)
            {
                // last in wins
                baseLineVersions[baseLinePackage.ItemSpec] = baseLinePackage.GetMetadata("Version");
            }

            List<ITaskItem> baseLinedDependencies = new List<ITaskItem>();

            foreach(var dependency in OriginalDependencies)
            {
                string baseLineVersion = null;
                if (baseLineVersions.TryGetValue(dependency.ItemSpec, out baseLineVersion))
                {
                    dependency.SetMetadata("Version", baseLineVersion);
                }
                baseLinedDependencies.Add(dependency);
            }

            BaseLinedDependencies = baseLinedDependencies.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
