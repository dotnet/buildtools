// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
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
            Dictionary<string, Version> baseLineVersions = new Dictionary<string, Version>();
            foreach(var baseLinePackage in BaseLinePackages)
            {
                // last in wins
                baseLineVersions[baseLinePackage.ItemSpec] = new Version(baseLinePackage.GetMetadata("Version"));
            }

            List<ITaskItem> baseLinedDependencies = new List<ITaskItem>();

            foreach(var dependency in OriginalDependencies)
            {
                Version baseLineVersion = null;
                Version requestedVersion = null;
                Version.TryParse(dependency.GetMetadata("Version"), out requestedVersion);

                if (baseLineVersions.TryGetValue(dependency.ItemSpec, out baseLineVersion) &&
                    (requestedVersion == null || baseLineVersion > requestedVersion))
                {
                    dependency.SetMetadata("Version", baseLineVersion.ToString(3));
                }
                baseLinedDependencies.Add(dependency);
            }

            BaseLinedDependencies = baseLinedDependencies.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
