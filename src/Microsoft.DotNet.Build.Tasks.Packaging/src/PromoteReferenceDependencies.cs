// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// Promotes dependencies from reference (ref) assembly TargetFramework to the implementation (lib) assembly 
    /// TargetFramework.  NuGet only ever chooses a single dependencyGroup from a package.  Often the TFM of the 
    /// implementation and reference differ so in order to ensure the dependencies from the reference assembly are
    /// also applicable in all circumstances that the implementation is applicable we need to copy the reference
    /// dependencies to the implementation group.  Usually this is redundant since the implementation must expose
    /// the surface area of the reference, but it is needed in two circumstances:
    ///   1. The implementation is a facade and doesn't reference the contracts.
    ///   2. We supress implementation dependencies with exclude=compile, but persist the reference dependencies.
    /// </summary>
    public class PromoteReferenceDependencies : PackagingTask
    {
        private const string IsReferenceMetadataName = "IsReferenceAsset";
        private const string TargetFrameworkMetadataName = "TargetFramework";

        [Required]
        public ITaskItem[] Dependencies { get; set; }
        
        [Required]
        public string FrameworkListsPath { get; set; }

        [Output]
        public ITaskItem[] PromotedDependencies { get; set; }
        
        public override bool Execute()
        {
            List<ITaskItem> promotedDependencies = new List<ITaskItem>();

            var implementationFxs = Dependencies.Select(d => d.GetMetadata("TargetFramework")).Distinct();

            var actualDependencies = Dependencies.Where(d => d.ItemSpec != "_._").Select(d => new Dependency(d)).ToArray();
            var referenceSets = actualDependencies.Where(d => d.IsReference).GroupBy(d => d.TargetFramework).ToDictionary(g => NuGetFramework.Parse(g.Key), g => g.ToArray());
            var candidateFxs = referenceSets.Keys.ToArray();

            if (candidateFxs.Length != 0)
            {
                foreach (var implementationFx in implementationFxs)
                {
                    NuGetFramework fx = NuGetFramework.Parse(implementationFx);
                    if (referenceSets.ContainsKey(fx))
                    {
                        // if this implementation assembly fx  has a matching reference fx skip promotion
                        continue;
                    }

                    var nearestReferenceFx = FrameworkUtilities.GetNearest(fx, candidateFxs);

                    if (nearestReferenceFx == null)
                    {
                        // This should never happen and indicates a bug in the package.  If a package contains references,
                        // all implementations should have an applicable reference assembly.
                        Log.LogError($"Could not find applicable reference assembly for implementation framework {implementationFx} from reference frameworks {string.Join(", ", referenceSets.Keys)}");
                    }

                    foreach (var reference in referenceSets[nearestReferenceFx])
                    {
                        var promotedDependency = new TaskItem(reference.OriginalItem);
                        promotedDependency.SetMetadata(TargetFrameworkMetadataName, implementationFx);

                        if (!Frameworks.IsInbox(FrameworkListsPath, implementationFx, reference.Id, reference.Version))
                        {
                            promotedDependencies.Add(promotedDependency);
                        }
                    }
                }
            }

            PromotedDependencies = promotedDependencies.ToArray();

            return !Log.HasLoggedErrors;
        }

        private class Dependency
        {
            public Dependency(ITaskItem item)
            {
                Id = item.ItemSpec;
                Version = item.GetMetadata("Version");
                bool isReference = false;
                bool.TryParse(item.GetMetadata(IsReferenceMetadataName), out isReference);
                IsReference = isReference;
                TargetFramework = item.GetMetadata(TargetFrameworkMetadataName);
                OriginalItem = item;
            }

            public string Id { get; }
            public string Version { get; }

            public bool IsReference { get; }
            public string TargetFramework { get; }

            public ITaskItem OriginalItem { get; }
        }
    }
}
