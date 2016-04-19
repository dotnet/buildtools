// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// Raises dependencies to a baseline version.
    /// Dependencies specified without a version will be raised to the highest permitted version.
    /// Dependencies with a version will be raised to the lowest baseline version that satisfies
    /// the requested version.
    /// </summary>
    public class ApplyBaseLine : PackagingTask
    {
        [Required]
        public ITaskItem[] OriginalDependencies { get; set; }

        /// <summary>
        /// Permitted package baseline versions.
        /// </summary>
        [Required]
        public ITaskItem[] BaseLinePackages { get; set; }
        
        [Output]
        public ITaskItem[] BaseLinedDependencies { get; set; }
        
        public override bool Execute()
        {
            Dictionary<string, SortedSet<Version>> baseLineVersions = new Dictionary<string, SortedSet<Version>>();
            foreach(var baseLinePackage in BaseLinePackages)
            {
                SortedSet<Version> versions = null;
                if (!baseLineVersions.TryGetValue(baseLinePackage.ItemSpec, out versions))
                {
                    baseLineVersions[baseLinePackage.ItemSpec] = versions = new SortedSet<Version>();
                }
                versions.Add(new Version(baseLinePackage.GetMetadata("Version")));
            }

            List<ITaskItem> baseLinedDependencies = new List<ITaskItem>();

            foreach(var dependency in OriginalDependencies)
            {
                SortedSet<Version> dependencyBaseLineVersions = null;
                Version requestedVersion = null;
                Version.TryParse(dependency.GetMetadata("Version"), out requestedVersion);

                if (baseLineVersions.TryGetValue(dependency.ItemSpec, out dependencyBaseLineVersions))
                {
                    // if no version is requested, choose the highest.  Otherwise choose the first that is 
                    // greater than or equal to the version requested.
                    Version baseLineVersion = requestedVersion == null ?
                        dependencyBaseLineVersions.Last() :
                        dependencyBaseLineVersions.FirstOrDefault(v => v >= requestedVersion);

                    if (baseLineVersion != null)
                    {
                        dependency.SetMetadata("Version", baseLineVersion.ToString(3));
                    }
                }
                baseLinedDependencies.Add(dependency);
            }

            BaseLinedDependencies = baseLinedDependencies.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
