// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Dependencies.Submodule
{
    /// <summary>
    /// Downloads a package specified from a dependency build info and updates the target git
    /// submodule to match the version inside.
    /// </summary>
    public class IndicatorPackageSubmoduleUpdater : SubmoduleUpdater
    {
        private static readonly Lazy<HttpClient> DownloadClient = new Lazy<HttpClient>();

        /// <summary>
        /// The NuGet v2 base url to use to download the indicator package, without a trailing '/'.
        /// For example, https://dotnet.myget.org/F/dotnet-core/api/v2/package.
        /// </summary>
        public string PackageDownloadBaseUrl { get; set; }

        public string IndicatorPackageId { get; }

        public IndicatorPackageSubmoduleUpdater(string indicatorPackageId)
        {
            if (indicatorPackageId == null)
            {
                throw new ArgumentNullException(nameof(indicatorPackageId), "An indicator package must be specified.");
            }
            IndicatorPackageId = indicatorPackageId;
        }

        protected override string GetDesiredCommitHash(
            IEnumerable<DependencyBuildInfo> dependencyBuildInfos,
            out IEnumerable<DependencyBuildInfo> usedBuildInfos)
        {
            foreach (var info in dependencyBuildInfos)
            {
                PackageIdentity package = info.Packages
                    .FirstOrDefault(p => p.Id == IndicatorPackageId);

                if (package == null)
                {
                    continue;
                }

                using (ZipArchive archive = DownloadPackageAsync(info, package).Result)
                {
                    ZipArchiveEntry versionTxtEntry = archive.GetEntry("version.txt");
                    if (versionTxtEntry == null)
                    {
                        Trace.TraceWarning(
                            $"Downloaded '{package}' in '{info.BuildInfo.Name}' " +
                            $"to upgrade '{Path}', but it had no version.txt file. Skipping.");
                        continue;
                    }
                    using (Stream versionTxt = versionTxtEntry.Open())
                    using (var versionTxtReader = new StreamReader(versionTxt))
                    {
                        string packageCommitHash = versionTxtReader.ReadLine();
                        Trace.TraceInformation($"Found commit '{packageCommitHash}' in versions.txt.");

                        usedBuildInfos = new[] { info };
                        return packageCommitHash;
                    }
                }
            }

            Trace.TraceError($"Failed to find '{IndicatorPackageId}' specifying a commit in any build-info.");
            usedBuildInfos = Enumerable.Empty<DependencyBuildInfo>();
            return null;
        }

        protected async Task<ZipArchive> DownloadPackageAsync(DependencyBuildInfo info, PackageIdentity package)
        {
            if (PackageDownloadBaseUrl == null)
            {
                throw new NotSupportedException(
                    $"A {nameof(PackageDownloadBaseUrl)} must be configured, " +
                    "as build-infos do not have package feed details.");
            }

            string downloadUrl = $"{PackageDownloadBaseUrl}/package/{package.Id}/{package.Version}";
            Trace.TraceInformation($"Downloading '{package}' from '{downloadUrl}'");

            HttpClient client = DownloadClient.Value;
            Stream nupkgStream = await client.GetStreamAsync(downloadUrl);

            return new ZipArchive(nupkgStream);
        }
    }
}
