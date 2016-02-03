// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using NuGet;
using NuGet.Versioning;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class NuGetPack : PackagingTask
    {

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

                try
                {
                    PackageBuilder builder = new PackageBuilder();

                    using (var nuspecFile = File.Open(nuspecPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        string baseDirectoryPath = (string.IsNullOrEmpty(BaseDirectory)) ? Path.GetDirectoryName(nuspecPath) : BaseDirectory;
                        Manifest manifest = Manifest.ReadFrom(nuspecFile);
                        builder.Populate(manifest.Metadata);
                        builder.PopulateFiles(baseDirectoryPath, manifest.Files);
                    }

                    // Overriding the Version from the Metadata if one gets passed in.
                    if (!string.IsNullOrEmpty(PackageVersion))
                    {
                        NuGetVersion overrideVersion;
                        if (NuGetVersion.TryParse(PackageVersion,out overrideVersion))
                        {
                            builder.Version = overrideVersion;
                        }
                        else
                        {
                            Log.LogError($"Failed to parse Package Version: '{PackageVersion}' is not a valid version.");
                            continue;
                        }
                    }

                    string id = builder.Id, version = builder.Version.ToString();

                    if (String.IsNullOrEmpty(id))
                    {
                        Log.LogError($"Nuspec {nuspecPath} does not contain a valid Id");
                        continue;
                    }

                    if (String.IsNullOrEmpty(version))
                    {
                        Log.LogError($"Nuspec {nuspecPath} does not contain a valid version");
                        continue;
                    }

                    string nupkgPath = Path.Combine(OutputDirectory, $"{id}.{version}.nupkg");

                    using (var fileStream = File.Create(nupkgPath))
                    {
                        builder.Save(fileStream);
                    }

                    Log.LogMessage($"Created '{nupkgPath}'");
                }
                catch (Exception e)
                {
                    Log.LogError($"Error when creating nuget package from {nuspecPath}. {e}");
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
