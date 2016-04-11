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
    public class CreateTrimDependencyGroups : PackagingTask
    {
        [Required]
        public string FrameworkListsPath
        {
            get;
            set;
        }

        [Required]
        public ITaskItem[] Dependencies
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

        [Output]
        public ITaskItem[] TrimmedDependencies
        {
            get;
            set;
        }

        public bool UseNetPlatform { get; set; }

        /* Given a set of available frameworks ("InboxOnTargetFrameworks"), and a list of desired frameworks,
        reduce the set of frameworks to the minimum set of frameworks which is compatible (preferring inbox frameworks. */
        public override bool Execute()
        {
            if (null == Dependencies)
            {
                Log.LogError("Dependencies argument must be specified");
                return false;
            }
            if (null == FrameworkListsPath)
            {
                Log.LogError("FrameworkListsPath argument must be specified");
                return false;
            }

            // Retrieve the list of generation dependency group TFM's
            var dependencyGroups = Dependencies.GroupBy(d => d.GetMetadata("TargetFramework")).Select(dg => new
                {
                    Framework = NuGetFramework.Parse(dg.Key),
                    Dependencies = dg.ToArray()
                });

            List<ITaskItem> addedDependencies = new List<ITaskItem>();

            // Exclude any non-portable frameworks that already have specific dependency groups.
            var frameworksToExclude = dependencyGroups.Select(dg => dg.Framework).Where(fx => !FrameworkUtilities.IsGenerationMoniker(fx));

            // Prepare a resolver for evaluating if candidate frameworks are actually supported by the package
            PackageItem[] packageItems = Files.Select(f => new PackageItem(f)).ToArray();
            var packagePaths = packageItems.Select(pi => pi.TargetPath);
            var targetFrameworksWithPlaceHolders = packageItems.Where(pi => NuGetAssetResolver.IsPlaceholder(pi.TargetPath)).Select(pi => pi.TargetFramework);

            NuGetAssetResolver resolver = new NuGetAssetResolver(null, packagePaths);

            foreach (var portableDependencyGroup in dependencyGroups.Where(dg => FrameworkUtilities.IsGenerationMoniker(dg.Framework)))
            {
                // Determine inbox frameworks for this generation that don't already have explicit groups
                HashSet<NuGetFramework> inboxFrameworksList = new HashSet<NuGetFramework>(
                    Frameworks.GetAlllInboxFrameworks(FrameworkListsPath)
                    .Where(fx => !fx.IsPCL)
                    .Where(fx => Generations.DetermineGenerationForFramework(fx, UseNetPlatform) >= portableDependencyGroup.Framework.Version &&
                        !frameworksToExclude.Any(exFx => exFx.Framework == fx.Framework && exFx.Version <= fx.Version)));
                
                // Check for assets which have a ref, but not a lib asset. If we have any of these, then they are actually not supported frameworks 
                // and we should not include them.
                inboxFrameworksList.RemoveWhere(inboxFx => !IsSupported(inboxFx, resolver));

                // Only add the lowest version for a particular inbox framework.  EG if both net45 and net46 are supported by this generation,
                //        only add net45
                inboxFrameworksList.RemoveWhere(fx => inboxFrameworksList.Any(otherFx => (otherFx.Framework.Equals(fx.Framework)) && (otherFx.Version < fx.Version)));
                
                // Create dependency items for each inbox framework.
                foreach (var framework in inboxFrameworksList)
                {
                    bool addedDependencyToFramework = false;
                    foreach (ITaskItem dependency in portableDependencyGroup.Dependencies)
                    {
                        string version = GetVersion(dependency);
                        
                        if (!Frameworks.IsInbox(FrameworkListsPath, framework, dependency.ItemSpec, version))
                        {
                            addedDependencyToFramework = true;
                            AddDependency(addedDependencies, new TaskItem(dependency), framework, portableDependencyGroup.Framework);
                        }
                    }
                    if (!addedDependencyToFramework)
                    {
                        AddDependency(addedDependencies, new TaskItem("_._"), framework, portableDependencyGroup.Framework);
                    }
                }
            }

            // Collapse frameworks
            // For any dependency with a targetframework, if there is another target framework which is compatible and older, remove this dependency.

            // Get all Dependencies which are not in a portable dependency group so that we can collapse the frameworks.  If we include
            // the portable frameworks, then we'll end up collapsing to those.
            List<NuGetFramework> allDependencyGroups = new List<NuGetFramework>();
            allDependencyGroups.AddRange(Dependencies.Select(d => NuGetFramework.Parse(d.GetMetadata("TargetFramework"))).Where(a => !allDependencyGroups.Contains(a) &&
                                                                  !FrameworkUtilities.IsGenerationMoniker(a) &&
                                                                  !FrameworkUtilities.IsPortableMoniker(a)));
            allDependencyGroups.AddRange(addedDependencies.Select(d => NuGetFramework.Parse(d.GetMetadata("TargetFramework"))).Where(a => !allDependencyGroups.Contains(a) &&
                                                                  !FrameworkUtilities.IsGenerationMoniker(a) &&
                                                                  !FrameworkUtilities.IsPortableMoniker(a)));

            List<NuGetFramework> collapsedDependencyGroups = FrameworkUtilities.ReduceDownwards(allDependencyGroups).ToList<NuGetFramework>();

            // Get the list of dependency groups that we collapsed down so that we can add them back if they contained different dependencies than what is present in the collapsed group.
            /* TODO: Look into NuGet's sorting algorithm, they may have a bug (fixed in this line). They were not including version in the sort.  
                     See ReduceCore in https://github.com/NuGet/NuGet.Client/blob/23ea68b91a439fcfd7f94bcd01bcdee2e8adae92/src/NuGet.Core/NuGet.Frameworks/FrameworkReducer.cs */
            IEnumerable<NuGetFramework> removedDependencyGroups = allDependencyGroups.Where(d => !collapsedDependencyGroups.Contains(d))?.OrderBy(f => f.Framework, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Version);
            foreach (NuGetFramework removedDependencyGroup in removedDependencyGroups)
            {
                // always recalculate collapsedDependencyGroups in case we added an item in a previous iteration.  Dependency groups are sorted, so this should be additive and we shouldn't need to restart the collapse / add back cycle
                var nearest = FrameworkUtilities.GetNearest(removedDependencyGroup, collapsedDependencyGroups.ToArray());

                // gather the dependencies for this dependency group and the calculated "nearest" dependency group
                var nearestDependencies = addedDependencies.Where(d => nearest.Equals(NuGetFramework.Parse(d.GetMetadata("TargetFramework")))).OrderBy(f => f.ToString());
                var currentDependencies = addedDependencies.Where(d => removedDependencyGroup.Equals(NuGetFramework.Parse(d.GetMetadata("TargetFramework")))).OrderBy(f => f.ToString());

                // The nearest dependency group's dependencies are different than this dependency group's dependencies
                if (nearestDependencies.Count() != currentDependencies.Count())
                {
                    // ignore if dependency is a placeholder
                    if (currentDependencies.Count() > 0)
                    {
                        if (!NuGetAssetResolver.IsPlaceholder(currentDependencies.First().ToString()))
                        {
                            collapsedDependencyGroups.Add(removedDependencyGroup);
                        }
                    }
                    else
                    {
                        collapsedDependencyGroups.Add(removedDependencyGroup);
                    }
                }
                // identical dependency count between current and nearest, and the count is > 0
                else if (nearestDependencies.Count() > 0)
                {
                    if (!currentDependencies.SequenceEqual(nearestDependencies, new DependencyITaskItemComparer()))
                    {
                        collapsedDependencyGroups.Add(removedDependencyGroup);
                    }
                }
            }


            List<ITaskItem> collapsedDependencies = new List<ITaskItem>();
            foreach (ITaskItem dependency in addedDependencies)
            {
                if (collapsedDependencyGroups.Contains(NuGetFramework.Parse(dependency.GetMetadata("TargetFramework"))))
                {
                    collapsedDependencies.Add(dependency);
                }
            }
            TrimmedDependencies = collapsedDependencies.ToArray();

            return !Log.HasLoggedErrors;
        }

        private bool IsSupported(NuGetFramework inboxFx, NuGetAssetResolver resolver)
        {
            var compileAssets = resolver.ResolveCompileAssets(inboxFx);

            // We assume that packages will only support inbox frameworks with lib/tfm assets and not runtime specific assets.
            // This effectively means we'll never reduce dependencies if a package happens to support an inbox framework with
            // a RID asset, but that is OK because RID assets can only be used by nuget3 + project.json
            // and we don't care about reducing dependencies for project.json because indirect dependencies are hidden.
            var runtimeAssets = resolver.ResolveRuntimeAssets(inboxFx, null);

            foreach (var compileAsset in compileAssets.Where(c => !NuGetAssetResolver.IsPlaceholder(c)))
            {
                string fileName = Path.GetFileName(compileAsset);

                if (!runtimeAssets.Any(r => Path.GetFileName(r).Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    // ref with no matching lib
                    return false;
                }
            }

            // Either all compile assets had matching runtime assets, or all were placeholders, make sure we have at
            // least one runtime asset to cover the placeholder case
            return runtimeAssets.Any();
        }

        private static string GetVersion(ITaskItem dependency)
        {
            // If we don't have the AssemblyVersion metadata (4 part version string), fall back and use Version (3 part version string)
            string version = dependency.GetMetadata("AssemblyVersion");
            if (string.IsNullOrEmpty(version))
            {
                version = dependency.GetMetadata("Version");

                int prereleaseIndex = version.IndexOf('-');
                if (prereleaseIndex != -1)
                {
                    version = version.Substring(0, prereleaseIndex);
                }
            }
            return version;
        }

        private static void AddDependency(List<ITaskItem> dependencies, ITaskItem item, NuGetFramework targetFramework, NuGetFramework generation)
        {
            item.SetMetadata("TargetFramework", targetFramework.GetShortFolderName());
            // "Generation" is not required metadata, we just include it because it can be useful for debugging.
            item.SetMetadata("Generation", generation.GetShortFolderName());
            dependencies.Add(item);
        }
    }
    public class DependencyITaskItemComparer : IEqualityComparer<ITaskItem>
    {
        public bool Equals(ITaskItem x, ITaskItem y)
        {
            if (x.ToString().Equals(y.ToString()) &&
                x.GetMetadata("Version") == y.GetMetadata("Version"))
            {
                return true;
            }
            return false;
        }

        public int GetHashCode(ITaskItem dependency)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(dependency, null)) return 0;

            int hashDependencyName = dependency.GetHashCode();

            int hashDependencyVersion = dependency.GetMetadata("Version").GetHashCode();

            //Calculate the hash code for the NuGetFramework.
            return hashDependencyName ^ hashDependencyVersion;
        }
    }
}
