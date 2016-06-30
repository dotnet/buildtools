// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools
{
    public class BuildInfo
    {
        public string Name { get; set; }

        public IEnumerable<PackageIdentity> LatestPackages { get; set; }

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
            var packages = new List<PackageIdentity>();

            string rawLatestUrl = $"{rawBuildInfoBaseUrl}/Latest.txt";
            string rawLatestPackagesUrl = $"{rawBuildInfoBaseUrl}/Latest_Packages.txt";

            using (Stream versionsStream = await client.GetStreamAsync(rawLatestPackagesUrl))
            using (StreamReader reader = new StreamReader(versionsStream))
            {
                string currentLine;
                while ((currentLine = await reader.ReadLineAsync()) != null)
                {
                    int spaceIndex = currentLine.IndexOf(' ');

                    string id = currentLine.Substring(0, spaceIndex);
                    var version = new NuGetVersion(currentLine.Substring(spaceIndex + 1));

                    packages.Add(new PackageIdentity(id, version));
                }
            }

            string releaseVersion;

            if (fetchLatestReleaseFile)
            {
                releaseVersion = (await client.GetStringAsync(rawLatestUrl)).Trim();
            }
            else
            {
                releaseVersion = packages
                    .Where(p => p.Version.IsPrerelease)
                    .Select(p => p.Version.Release)
                    .FirstOrDefault()
                    ??
                    // if there are no prerelease versions, just grab the first version
                    packages
                        .Select(p => p.Version.ToNormalizedString())
                        .FirstOrDefault();
            }

            return new BuildInfo
            {
                Name = name,
                LatestPackages = packages,
                LatestReleaseVersion = releaseVersion
            };
        }
    }
}
