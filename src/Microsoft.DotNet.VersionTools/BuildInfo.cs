// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.DotNet.VersionTools
{
    public class BuildInfo
    {
        public string Name { get; set; }

        public Dictionary<string, string> LatestPackages { get; set; }

        public string LatestReleaseVersion { get; set; }

        public static BuildInfo Get(
            string name,
            string rawBuildInfoBaseUrl,
            bool fetchLatestReleaseFile = true)
        {
            using (var client = new HttpClient())
            {
                return GetAsync(
                    client,
                    name,
                    rawBuildInfoBaseUrl,
                    fetchLatestReleaseFile).Result;
            }
        }

        public static async Task<BuildInfo> GetAsync(
            HttpClient client,
            string name,
            string rawBuildInfoBaseUrl,
            bool fetchLatestReleaseFile = true)
        {
            Dictionary<string, string> packages;

            string rawLatestUrl = $"{rawBuildInfoBaseUrl}/Latest.txt";
            string rawLatestPackagesUrl = $"{rawBuildInfoBaseUrl}/Latest_Packages.txt";

            using (Stream versionsStream = await client.GetStreamAsync(rawLatestPackagesUrl))
            using (StreamReader reader = new StreamReader(versionsStream))
            {
                packages = await ReadPackageListAsync(reader);
            }

            string releaseVersion;

            if (fetchLatestReleaseFile)
            {
                releaseVersion = (await client.GetStringAsync(rawLatestUrl)).Trim();
            }
            else
            {
                var versions = packages.Values
                    .Select(versionString => new NuGetVersion(versionString));

                releaseVersion =
                    versions.FirstOrDefault(v => v.IsPrerelease)?.Release ??
                    // if there are no prerelease versions, just grab the first version
                    versions.FirstOrDefault()?.ToNormalizedString();
            }

            return new BuildInfo
            {
                Name = name,
                LatestPackages = packages,
                LatestReleaseVersion = releaseVersion
            };
        }

        public static BuildInfo CachedGet(
            string name,
            string rawRepoUrl,
            string gitRef,
            string buildInfoPath,
            string cacheDir,
            bool fetchLatestReleaseFile = true)
        {
            // Check if the ref is a commit hash. If it's a branch name, it can't be cached.
            // A branch on GitHub can't have a name like this: GitHub refuses the push with
            // "GH002: Sorry, branch or tag names consisting of 40 hex characters are not allowed."
            bool useCache = !string.IsNullOrEmpty(cacheDir) &&
                gitRef.Length == 40 &&
                gitRef.All("0123456789abcdef".Contains);

            string cachedPath = useCache
                ? Path.Combine(cacheDir, gitRef, name, "buildinfo.json")
                : null;

            if (useCache && File.Exists(cachedPath))
            {
                try
                {
                    return JsonConvert.DeserializeObject<BuildInfo>(File.ReadAllText(cachedPath));
                }
                catch (Exception e)
                {
                    Trace.TraceWarning(
                        $"Couldn't read build info from cache '{cachedPath}'. Redownloading. " +
                        $"Exception caught: {e}");
                }
            }

            BuildInfo info = Get(
                name,
                RawBuildInfoBaseUrl(rawRepoUrl, gitRef, buildInfoPath),
                fetchLatestReleaseFile);

            if (useCache)
            {
                Directory.GetParent(cachedPath).Create();
                File.WriteAllText(cachedPath, JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            return info;
        }

        public static string RawBuildInfoBaseUrl(string rawRepoUrl, string gitRef, string buildInfoPath)
        {
            return $"{rawRepoUrl}/{gitRef}/{buildInfoPath}";
        }

        private static async Task<Dictionary<string, string>> ReadPackageListAsync(TextReader reader)
        {
            var packages = new Dictionary<string, string>();
            string currentLine;
            while ((currentLine = await reader.ReadLineAsync()) != null)
            {
                int spaceIndex = currentLine.IndexOf(' ');

                string id = currentLine.Substring(0, spaceIndex);
                string version = currentLine.Substring(spaceIndex + 1);

                if (packages.ContainsKey(id))
                {
                    throw new Exception($"More than one package list entry with id '{id}'.");
                }
                packages[id] = version;
            }
            return packages;
        }
    }
}
