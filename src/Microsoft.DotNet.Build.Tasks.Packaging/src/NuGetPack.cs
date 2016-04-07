// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using NuGet;
using NuGet.Versioning;
using NuGet.Packaging;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class NuGetPack : PackagingTask
    {
        /// <summary>
        /// Target file paths to exclude when building the lib package for symbol server scenario
        /// Copied from https://github.com/NuGet/NuGet.Client/blob/59433c7bacaae435a2cfe343cd441ea710579304/src/NuGet.Core/NuGet.Commands/PackCommandRunner.cs#L48
        /// </summary>
        private static readonly string[] _libPackageExcludes = new[] {
            @"**\*.pdb".Replace('\\', Path.DirectorySeparatorChar),
            @"src\**\*".Replace('\\', Path.DirectorySeparatorChar)
        };

        /// <summary>
        /// Target file paths to exclude when building the symbols package for symbol server scenario
        /// </summary>
        private static readonly string[] _symbolPackageExcludes = new[] {
            @"content\**\*".Replace('\\', Path.DirectorySeparatorChar),
            @"tools\**\*.ps1".Replace('\\', Path.DirectorySeparatorChar)
        };

        [Required]
        public ITaskItem[] Nuspecs
        {
            get;
            set;
        }

        [Required]
        public string OutputDirectory
        {
            get;
            set;
        }

        public string BaseDirectory
        {
            get;
            set;
        }

        public string PackageVersion
        {
            get;
            set;
        }

        public bool ExcludeEmptyDirectories
        {
            get;
            set;
        }

        public bool PackSymbolPackage
        {
            get;
            set;
        }

        public ITaskItem[] AdditionalLibPackageExcludes
        {
            get;
            set;
        }

        public ITaskItem[] AdditionalSymbolPackageExcludes
        {
            get;
            set;
        }

        /// <summary>
        /// If set, the symbol package is placed in the given directory. Otherwise OutputDirectory is used.
        /// </summary>
        public string SymbolPackageOutputDirectory
        {
            get;
            set;
        }

        public override bool Execute()
        {
            if (Nuspecs == null || Nuspecs.Length == 0)
            {
                Log.LogError("Nuspecs argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(OutputDirectory))
            {
                Log.LogError("OuputDirectory argument must be specified");
                return false;
            }

            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            foreach (var nuspec in Nuspecs)
            {
                string nuspecPath = nuspec.GetMetadata("FullPath");

                if (!File.Exists(nuspecPath))
                {
                    Log.LogError($"Nuspec {nuspecPath} does not exist");
                    continue;
                }

                Pack(nuspecPath, false);

                if (PackSymbolPackage)
                {
                    Pack(nuspecPath, true);
                }
            }

            return !Log.HasLoggedErrors;
        }

        public void Pack(string nuspecPath, bool packSymbols)
        {
            try
            {
                PackageBuilder builder = new PackageBuilder();

                using (var nuspecFile = File.Open(nuspecPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    string baseDirectoryPath = (string.IsNullOrEmpty(BaseDirectory)) ? Path.GetDirectoryName(nuspecPath) : BaseDirectory;
                    Manifest manifest = Manifest.ReadFrom(nuspecFile, false);
                    builder.Populate(manifest.Metadata);
                    builder.PopulateFiles(baseDirectoryPath, manifest.Files);

                    PathResolver.FilterPackageFiles(
                        builder.Files,
                        file => file.Path,
                        packSymbols ? SymbolPackageExcludes : LibPackageExcludes);
                }

                if (packSymbols)
                {
                    // Symbol packages are only valid if they contain both symbols and sources.
                    Dictionary<string, bool> pathHasMatches = LibPackageExcludes.ToDictionary(
                        path => path,
                        path => PathResolver.GetMatches(builder.Files, file => file.Path, new[] { path }).Any());

                    if (!pathHasMatches.Values.Any(i => i))
                    {
                        Log.LogMessage(LogImportance.Low, $"Nuspec {nuspecPath} does not contain symbol or source files. Not creating symbol package.");
                        return;
                    }
                    foreach (var pathPair in pathHasMatches.Where(pathMatchPair => !pathMatchPair.Value))
                    {
                        Log.LogMessage(LogImportance.Low, $"Nuspec {nuspecPath} does not contain any files matching {pathPair.Key}. Not creating symbol package.");
                        return;
                    }
                }

                // Overriding the Version from the Metadata if one gets passed in.
                if (!string.IsNullOrEmpty(PackageVersion))
                {
                    NuGetVersion overrideVersion;
                    if (NuGetVersion.TryParse(PackageVersion, out overrideVersion))
                    {
                        builder.Version = overrideVersion;
                    }
                    else
                    {
                        Log.LogError($"Failed to parse Package Version: '{PackageVersion}' is not a valid version.");
                        return;
                    }
                }

                string id = builder.Id, version = builder.Version.ToString();

                if (String.IsNullOrEmpty(id))
                {
                    Log.LogError($"Nuspec {nuspecPath} does not contain a valid Id");
                    return;
                }

                if (String.IsNullOrEmpty(version))
                {
                    Log.LogError($"Nuspec {nuspecPath} does not contain a valid version");
                    return;
                }

                string nupkgOutputDirectory = OutputDirectory;

                if (packSymbols && !string.IsNullOrEmpty(SymbolPackageOutputDirectory))
                {
                    nupkgOutputDirectory = SymbolPackageOutputDirectory;
                }

                string nupkgExtension = packSymbols ? ".symbols.nupkg" : ".nupkg";
                string nupkgPath = Path.Combine(nupkgOutputDirectory, $"{id}.{version}{nupkgExtension}");

                var directory = Path.GetDirectoryName(nupkgPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var fileStream = File.Create(nupkgPath))
                {
                    builder.Save(fileStream);
                }

                Log.LogMessage($"Created '{nupkgPath}'");
            }
            catch (Exception e)
            {
                string packageType = packSymbols ? "symbol" : "lib";
                Log.LogError($"Error when creating nuget {packageType} package from {nuspecPath}. {e}");
            }
        }

        private IEnumerable<string> LibPackageExcludes
        {
            get
            {
                return _libPackageExcludes
                    .Concat(AdditionalLibPackageExcludes?.Select(item => item.ItemSpec) ?? Enumerable.Empty<string>());
            }
        }

        private IEnumerable<string> SymbolPackageExcludes
        {
            get
            {
                return _symbolPackageExcludes
                    .Concat(AdditionalSymbolPackageExcludes?.Select(item => item.ItemSpec) ?? Enumerable.Empty<string>());
            }
        }
    }
}
