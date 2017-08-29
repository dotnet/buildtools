// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ComputeDestinationsForDependencies : BuildTask
    {
        [Required]
        public ITaskItem[] TestDependencies { get; set; }

        [Output]
        public ITaskItem[] TestDependenciesWithDestinations
        {
            get { return TestDependencies; }
        }

        public override bool Execute()
        {
            foreach (ITaskItem dependency in TestDependencies)
            {
                string relativeDestinationPath;

                bool? preserveSubDirectories = dependency.GetMetadata("PreserveSubDirectories")?.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (preserveSubDirectories == true)
                {
                    string packageRelativePath = dependency.GetMetadata("PackageRelativePath");
                    if (packageRelativePath == null)
                    {
                        throw new InvalidOperationException(
                            $"'PackageRelativePath' metadata missing for item {dependency.ItemSpec}");
                    }

                    // PackageRelativePath contains (PackageName\VersionNumber\[Directories\]FileName). This is to remove the first two directories on 
                    // the path to preserve just the directory structure.
                    string[] segments = packageRelativePath.Split(new Char[] { '/', '\\' });
                    if (segments.Length < 3)
                    {
                        throw new InvalidOperationException(
                            @"The PackageRelativePath must be in the format 'PackageName\VersionNumber\[Directories\]FileName'");
                    }

                    string[] relativeDestinationPathSegments = new string[segments.Length - 2];
                    Array.Copy(segments, 2, relativeDestinationPathSegments, 0, relativeDestinationPathSegments.Length);
                    relativeDestinationPath = string.Join(Path.DirectorySeparatorChar.ToString(), relativeDestinationPathSegments);
                }
                else
                {
                    relativeDestinationPath = Path.GetFileName(dependency.ItemSpec);
                }

                dependency.SetMetadata("RelativeDestinationPath", relativeDestinationPath);
            }

            return true;
        }
    }
}
