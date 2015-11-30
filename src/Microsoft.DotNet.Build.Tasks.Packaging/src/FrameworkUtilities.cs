// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class FrameworkUtilities
    {
        private static FrameworkReducer s_reducer;
        private static FrameworkReducer Reducer
        {
            get
            {
                if (s_reducer == null)
                {
                    s_reducer = new FrameworkReducer();
                }
                return s_reducer;
            }
        }

        public static bool IsGenerationMoniker(string dependencyGroup)
        {
            NuGetFramework dependencyFramework = NuGetFramework.Parse(dependencyGroup);
            return IsGenerationMoniker(dependencyFramework);
        }
        public static bool IsGenerationMoniker(NuGetFramework dependencyGroup)
        {
            var range = FrameworkConstants.DotNetGenerationRanges.DotNet55;
            return range.Satisfies(dependencyGroup);
        }
        public static bool IsPortableMoniker(NuGetFramework nuGetFramework)
        {
            return nuGetFramework == null ? false : nuGetFramework.GetShortFolderName().StartsWith("portable-");
        }
        public static Version Ensure4PartVersion(Version version)
        {
            if (version.Minor == -1 || version.Build == -1 || version.Revision == -1)
            {
                version = new Version(version.Major,
                                      version.Minor == -1 ? 0 : version.Minor,
                                      version.Build == -1 ? 0 : version.Build,
                                      version.Revision == -1 ? 0 : version.Revision);
            }
            return version;
        }
        public static string[] GetNearest(string[] frameworkNames, string[] frameworks)
        {
            List<string> nearestFrameworks = new List<string>();
            nearestFrameworks.AddRange(frameworkNames.Where(framework => (GetNearest(framework, frameworks) != null)));

            foreach (string frameworkName in frameworkNames)
            {
                string nearest = GetNearest(frameworkName, frameworks);
                if (nearest != null)
                    nearestFrameworks.Add(nearest);
            }
            return nearestFrameworks.ToArray();
        }
        public static string GetNearest(string frameworkName, string[] frameworks)
        {
            NuGetFramework nuGetFrameworkName = NuGetFramework.Parse(frameworkName);
            var _frameworks = frameworks.Select(s => NuGetFramework.Parse(s)).ToList();
            var nearest = Reducer.GetNearest(nuGetFrameworkName, _frameworks);
            if (nearest != null)
            {
                return nearest.GetShortFolderName();
            }
            return null;
        }
        public static NuGetFramework GetNearest(NuGetFramework framework, NuGetFramework[] frameworks)
        {
            return Reducer.GetNearest(framework, frameworks);
        }
        public static IEnumerable<NuGetFramework> ReduceDownwards(IEnumerable<NuGetFramework> frameworks)
        {
            return Reducer.ReduceDownwards(frameworks);
        }
    }
}
