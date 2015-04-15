// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NuGet.Build.Tasks
{
    internal sealed class MSBuildDependencyProvider : IDependencyProvider
    {
        private readonly string _msbuildProjectFilePath;
        private readonly string[] _additionalProjectJsonPaths;
        

        public MSBuildDependencyProvider(string projectFilePath, IEnumerable<string> additionalProjectJsonPaths)
        {
            _msbuildProjectFilePath = projectFilePath;
            _additionalProjectJsonPaths = additionalProjectJsonPaths == null ? new string[0] : additionalProjectJsonPaths.ToArray();
        }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            if (libraryRange.Name != _msbuildProjectFilePath)
            {
                return null;
            }

            var dependencies = new List<LibraryDependency>();

            var projectJsonPath = Path.Combine(Path.GetDirectoryName(_msbuildProjectFilePath), "project.json");

            if (File.Exists(projectJsonPath))
            {
                // may be running against a project with only a packages.config
                AddFromProjectJsonFile(dependencies, projectJsonPath, targetFramework);
            }

            foreach(string additionalProjectJsonPath in _additionalProjectJsonPaths)
            {
                AddFromProjectJsonFile(dependencies, additionalProjectJsonPath, targetFramework);
            }

            var description = new Library
            {
                LibraryRange = libraryRange,
                Identity = new LibraryIdentity
                {
                    Name = libraryRange.Name,
                    Version = new NuGetVersion(new Version(1, 0)), // TODO: Make up something better
                    Type = LibraryTypes.MSBuild,
                },
                Path = _msbuildProjectFilePath,
                Dependencies = dependencies
            };

            return description;
        }

        private void AddFromProjectJsonFile(List<LibraryDependency> dependencies, string projectJsonPath, NuGetFramework targetFramework)
        {
            using (var fileStream = new FileStream(projectJsonPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var packageSpec = JsonPackageSpecReader.GetPackageSpec(fileStream, projectJsonPath, projectJsonPath);

                // Grab dependencies from here too
                var targetFrameworkInfo = packageSpec.GetTargetFramework(targetFramework);

                dependencies.AddRange(packageSpec.Dependencies);
                dependencies.AddRange(targetFrameworkInfo.Dependencies);
            }
        }

        public bool SupportsType(string libraryType)
        {
            return libraryType == LibraryTypes.MSBuild || libraryType == null;
        }
    }
}
