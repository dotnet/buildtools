// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class PackageIndex
    {
        public static PackageIndex Current { get; } = new PackageIndex();

        public Dictionary<string, PackageInfo> Packages { get; set; } = new Dictionary<string, PackageInfo>();

        public Dictionary<string, string> ModulesToPackages { get; set; } = new Dictionary<string, string>();

        [JsonIgnore]
        public HashSet<string> IndexSources { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static PackageIndex Load(string packageIndexFile)
        {
            using (var file = File.OpenText(packageIndexFile))
            using (var jsonTextReader = new JsonTextReader(file))
            {
                var serializer = new JsonSerializer();
                serializer.Converters.Add(new VersionConverter());
                var result = serializer.Deserialize<PackageIndex>(jsonTextReader);
                result.IndexSources.Add(Path.GetFullPath(packageIndexFile));
                return result;
            }
        }

        public void Save(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var file = File.CreateText(path))
            {
                var serializer = new JsonSerializer();
                serializer.StringEscapeHandling = StringEscapeHandling.EscapeNonAscii;
                serializer.Formatting = Formatting.Indented;
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
                serializer.Converters.Add(new VersionConverter());
                serializer.Serialize(file, this);
            }
        }

        /// <summary>
        /// Merges an index into the currently loaded index if not already merged.
        /// </summary>
        /// <param name="otherIndexFile"></param>
        public void Merge(string otherIndexFile)
        {
            if (!IndexSources.Contains(otherIndexFile))
            {
                Merge(Load(otherIndexFile));
            }
        }

        /// <summary>
        /// Merges a list of indexes into the currently loaded index if not already merged.
        /// </summary>
        /// <param name="otherIndexFiles"></param>
        public void Merge(IEnumerable<string> otherIndexFiles)
        {
            foreach(var otherIndexFile in otherIndexFiles)
            {
                Merge(otherIndexFile);
            }
        }

        /// <summary>
        /// Merges another packageIndex into this package index.  For any overlapping
        /// data 'other' has precedence.
        /// </summary>
        /// <param name="other"></param>
        public void Merge(PackageIndex other)
        {
            if (other.IndexSources.IsSubsetOf(IndexSources))
            {
                return;
            }

            foreach(var otherPackage in other.Packages)
            {
                var otherInfo = otherPackage.Value;
                PackageInfo existingInfo;

                if (Packages.TryGetValue(otherPackage.Key, out existingInfo))
                {
                    existingInfo.Merge(otherInfo);
                }
                else
                {
                    Packages[otherPackage.Key] = otherInfo;
                }
            }

            foreach(var otherModuleToPackage in other.ModulesToPackages)
            {
                ModulesToPackages[otherModuleToPackage.Key] = otherModuleToPackage.Value;
            }

            foreach(var otherIndexSource in other.IndexSources)
            {
                IndexSources.Add(otherIndexSource);
            }
        }

        // helper functions
        public bool TryGetBaseLineVersion(string packageId, out Version baseLineVersion)
        {
            PackageInfo info;
            baseLineVersion = null;

            if (Packages.TryGetValue(packageId, out info))
            {
                baseLineVersion = info.BaselineVersion;
            }

            return baseLineVersion != null;
        }

        public bool IsStable(string packageId, Version packageVersion)
        {
            PackageInfo info;
            bool isStable = false;

            if (Packages.TryGetValue(packageId, out info))
            {
                isStable = info.StableVersions.Contains(packageVersion);
            }

            return isStable;
        }

        public Version GetPackageVersionForAssemblyVersion(string packageId, Version assemblyVersion)
        {
            PackageInfo info;
            Version packageVersion = null;

            if (assemblyVersion != null)
            {
                if (Packages.TryGetValue(packageId, out info))
                {
                    packageVersion = info.GetPackageVersionForAssemblyVersion(assemblyVersion);
                }
                else
                {
                    // if not found assume 1:1 with assembly version
                    packageVersion = VersionUtility.As3PartVersion(assemblyVersion);
                }
            }

            return packageVersion;
        }
    }

    public class PackageInfo
    {
        public HashSet<Version> StableVersions { get; set; } = new HashSet<Version>();

        public bool ShouldSerializeStableVersions() { return StableVersions.Count > 0; }

        public Version BaselineVersion { get; set; }

        public Dictionary<Version, Version> AssemblyVersionInPackageVersion { get; set; } = new Dictionary<Version, Version>();

        public bool ShouldSerializeAssemblyVersionInPackageVersion() { return AssemblyVersionInPackageVersion.Count > 0; }

        public void Merge(PackageInfo other)
        {
            StableVersions.UnionWith(other.StableVersions);

            if (other.BaselineVersion != null)
            {
                // prefer other over existing
                BaselineVersion = other.BaselineVersion;
            }

            foreach (var assemblyVersionInPackage in other.AssemblyVersionInPackageVersion)
            {
                Version otherAssemblyVersion = assemblyVersionInPackage.Key;
                Version otherPackageVersion = assemblyVersionInPackage.Value;

                AddAssemblyVersionInPackage(otherAssemblyVersion, otherPackageVersion);
            }
        }

        public void AddAssemblyVersionInPackage(Version assemblyVersion, Version packageVersion)
        {
            Version existingPackageVersion;
            if (AssemblyVersionInPackageVersion.TryGetValue(assemblyVersion, out existingPackageVersion))
            {
                // prefer the lowest versioned package which first exposed this API version
                if (existingPackageVersion > packageVersion)
                {
                    AssemblyVersionInPackageVersion[assemblyVersion] = packageVersion;
                }
            }
            else
            {
                AssemblyVersionInPackageVersion[assemblyVersion] = packageVersion;
            }
        }

        public Version GetPackageVersionForAssemblyVersion(Version assemblyVersion)
        {
            Version packageVersion = null;

            if (assemblyVersion != null)
            {
                // prefer an explicit mapping
                if (!AssemblyVersionInPackageVersion.TryGetValue(assemblyVersion, out packageVersion))
                {
                    // if not found assume 1:1 with assembly version
                    packageVersion = VersionUtility.As3PartVersion(assemblyVersion);
                }
            }

            return packageVersion;
        }

    }
}
