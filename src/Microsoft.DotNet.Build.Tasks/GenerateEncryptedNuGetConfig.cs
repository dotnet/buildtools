// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateEncryptedNuGetConfig : BuildTask
    {
        [Required]
        public string ConfigPath { get; set; }

        /// <summary>
        /// Sources to configure. ItemSpec is source name, required metadata "Uri" with optional
        /// "Username" and "Password".
        /// </summary>
        [Required]
        public ITaskItem[] Sources { get; set; }

        public string PackagesDir { get; set; }

        public override bool Execute()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            File.WriteAllText(ConfigPath, GenerateUnencryptedNuGetConfig.CreateXmlString(null));

            var settings = new Settings(
                Path.GetDirectoryName(ConfigPath),
                Path.GetFileName(ConfigPath));

            if (!string.IsNullOrEmpty(PackagesDir))
            {
                settings.SetValue("config", "globalPackagesFolder", Path.GetFullPath(PackagesDir));
            }

            var sourceProvider = new PackageSourceProvider(settings);

            // Add source with username and encrypted password.
            // See https://github.com/NuGet/NuGet.Client/blob/04f9b91fb/src/NuGet.Clients/NuGet.CommandLine/Commands/SourcesCommand.cs#L147-L168
            List<PackageSource> sourceList = sourceProvider.LoadPackageSources().ToList();

            foreach (ITaskItem newSourceItem in Sources)
            {
                string name = newSourceItem.ItemSpec;
                string uri = newSourceItem.GetMetadata("Uri");
                if (string.IsNullOrEmpty(uri))
                {
                    throw new ArgumentException(
                        $"Item '{newSourceItem}': 'Uri' metadata required.");
                }

                var newSource = new PackageSource(uri, name);

                string username = newSourceItem.GetMetadata("Username");
                string password = newSourceItem.GetMetadata("Password");

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    newSource.Credentials = PackageSourceCredential.FromUserInput(
                        name,
                        username,
                        password,
                        storePasswordInClearText: false);
                }

                sourceList.Add(newSource);
            }

            sourceProvider.SavePackageSources(sourceList);

            return !Log.HasLoggedErrors;
        }
    }
}
