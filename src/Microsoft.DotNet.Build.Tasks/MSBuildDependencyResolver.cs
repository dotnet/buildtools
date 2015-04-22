// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.NuGet.Build.Tasks
{
    internal sealed class MSBuildDependencyProvider : IDependencyProvider
    {
        private readonly IList<LibraryDependency> _dependencies;
        private readonly string _msbuildProjectFilePath;

        public MSBuildDependencyProvider(string projectFilePath, IEnumerable<ITaskItem> nugetPackageReferences)
        {
            _dependencies = new List<LibraryDependency>();

            foreach (var nugetPackageReference in nugetPackageReferences)
            {
                _dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = nugetPackageReference.ItemSpec,
                        VersionRange = VersionRange.Parse(nugetPackageReference.GetMetadata("VersionRange"))
                    },
                });
            }

            _msbuildProjectFilePath = projectFilePath;
        }

        public IEnumerable<string> GetAttemptedPaths(NuGetFramework targetFramework)
        {
            return Enumerable.Empty<string>();
        }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            if (libraryRange.Name != _msbuildProjectFilePath)
            {
                return null;
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
                Dependencies = _dependencies
            };

            return description;
        }

        public bool SupportsType(string libraryType)
        {
            return libraryType == LibraryTypes.MSBuild || libraryType == null;
        }
    }
}
