// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    public class LocatePreviousContract : BuildTask
    {
        [Required]
        public string CurrentContractProjectPath { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        [Output]
        public string PreviousContractVersion { get; set; }

        public override bool Execute()
        {
            // We trim the last zero as the folder names in the src/ref folders are truncated.
            // eg. AssemblyVersion 4.0.0.0 wil be <AssemblyName>/4.0.0
            AssemblyVersion = AssemblyVersion.Substring(0, AssemblyVersion.LastIndexOf('.'));
            string currentDir = Path.GetDirectoryName(CurrentContractProjectPath);
            
            Version currentVersion;
            Version maxPreviousVersion = null;

            if (!Version.TryParse(AssemblyVersion, out currentVersion))
            {
                return true;
            }

            foreach (string candidateDir in Directory.EnumerateDirectories(currentDir))
            {
                Version candidateVersion;
                if (Version.TryParse(Path.GetFileName(candidateDir), out candidateVersion)
                    && candidateVersion < currentVersion
                    && (maxPreviousVersion == null || candidateVersion > maxPreviousVersion))
                {
                    maxPreviousVersion = candidateVersion;
                }
            }

            if (maxPreviousVersion == null)
            {
                PreviousContractVersion = String.Empty;                
            }
            else
            {
                PreviousContractVersion = maxPreviousVersion.ToString();               
            }
            return true;
        }
    }
}
