// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed.BuildManifest
{
    public class WriteOrchestratedBuildManifestSummaryToFile : Task
    {
        private static readonly Dictionary<string, string> SdkFileEndingLinks = new Dictionary<string, string>
        {
            ["alpine.3.6-x64.tar.gz"] = "alpine-3.6-targz",
            ["linux-x64.tar.gz"] = "linux-targz",
            ["osx-x64.pkg"] = "osx-installer",
            ["osx-x64.tar.gz"] = "osx-targz",
            ["rhel-x64.rpm"] = "rhel-7-installer",
            ["rhel.6-x64.tar.gz"] = "rhel-6-targz",
            ["win-x64.exe"] = "win-x64-installer",
            ["win-x64.zip"] = "win-x64-zip",
            ["win-x86.exe"] = "win-x86-installer",
            ["win-x86.zip"] = "win-x86-zip",
            ["x64.deb"] = "linux-DEB-installer"
        };

        private const string SdkTableText = @"
| Platform | Build |
| -------- | :-------------------------------------: |
| **Windows x64** | [Installer][win-x64-installer] - [Checksum][win-x64-installer-checksum]<br>[zip][win-x64-zip] - [Checksum][win-x64-zip-checksum] |
| **Windows x86** | [Installer][win-x86-installer] - [Checksum][win-x86-installer-checksum]<br>[zip][win-x86-zip] - [Checksum][win-x86-zip-checksum] |
| **macOS** | [Installer][osx-installer] - [Checksum][osx-installer-checksum]<br>[tar.gz][osx-targz] - [Checksum][osx-targz-checksum] |
| **Linux x64** | [DEB Installer][linux-DEB-installer] - [Checksum][linux-DEB-installer-checksum]<br>[tar.gz][linux-targz] - [Checksum][linux-targz-checksum] |
| **RHEL 7.2** | [Installer][rhel-7-installer] - [Checksum][rhel-7-installer-checksum]<br>[tar.gz][linux-targz] - [Checksum][linux-targz-checksum] |
| **RHEL 6** | [tar.gz][rhel-6-targz] - [Checksum][rhel-6-targz-checksum] |
| **Alpine 3.6** | [tar.gz][alpine-3.6-targz] - [Checksum][alpine-3.6-targz-checksum] |";

        [Required]
        public string File { get; set; }

        [Required]
        public string ManifestFile { get; set; }

        public override bool Execute()
        {
            string contents = System.IO.File.ReadAllText(ManifestFile);
            OrchestratedBuildModel model = OrchestratedBuildModel.Parse(XElement.Parse(contents));

            var builder = new StringBuilder();

            builder.Append("## Product build: ");
            builder.AppendLine(model.Identity.ToString());

            builder.AppendLine();
            builder.AppendLine("### SDK Installers and Binaries");
            builder.AppendLine(SdkTableText);

            builder.AppendLine();
            EndpointModel blobFeed = model.Endpoints.First(e => e.IsOrchestratedBlobFeed);
            foreach (var link in blobFeed.Artifacts.Blobs
                .Select(b => new
                {
                    b.Id,
                    Description = GetSdkInstallerDescription(b)
                })
                .Where(d => d.Description != null))
            {
                builder.Append("[");
                builder.Append(link.Description);
                builder.Append("]: ");
                builder.Append(blobFeed.Url.Replace("/index.json", "/assets/"));
                builder.AppendLine(link.Id);
            }

            builder.AppendLine();
            builder.AppendLine("### Built Repositories");

            foreach (BuildIdentity build in model.Builds
                .Where(b => b.Name != "anonymous")
                .OrderBy(b => b.Name))
            {
                builder.Append(" * ");
                builder.AppendLine(build.ToString());
            }

            System.IO.File.WriteAllText(File, builder.ToString());

            return !Log.HasLoggedErrors;
        }

        private static string GetSdkInstallerDescription(BlobArtifactModel blob)
        {
            string shipInstaller;

            if (blob.Attributes.TryGetValue("ShipInstaller", out shipInstaller))
            {
                string[] blobPathParts = blob.Id.Split('/');

                if (blobPathParts.Length == 3 && blobPathParts[0] == "Sdk")
                {
                    string version = blobPathParts[1];
                    string[] fileParts = blobPathParts[2].Split(
                        new[] {$"-{version}-"},
                        StringSplitOptions.None);

                    if (fileParts.Length == 2 && fileParts[0] == "dotnet-sdk")
                    {
                        string linkText;

                        if (shipInstaller == "dotnetcli" &&
                            SdkFileEndingLinks.TryGetValue(fileParts[1], out linkText))
                        {
                            return linkText;
                        }

                        if (shipInstaller == "dotnetclichecksums" &&
                            SdkFileEndingLinks.TryGetValue(fileParts[1].Replace(".sha", ""), out linkText))
                        {
                            return $"{linkText}-checksum";
                        }
                    }
                }
            }

            return null;
        }
    }
}
