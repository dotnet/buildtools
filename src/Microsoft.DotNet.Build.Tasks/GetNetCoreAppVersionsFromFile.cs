// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetNetCoreAppVersionsFromFile : Task
    {
        [Required]
        public string PathToVersionsFile { get; set; }

        [Output]
        public string CorefxHash { get; set; }

        [Output]
        public string DotnetVersionsCorefxHash { get; set; }

        [Output]
        public string CoreclrHash { get; set; }

        [Output]
        public string DotnetVersionsCoreclrHash { get; set; }

        [Output]
        public string CoresetupHash { get; set; }

        [Output]
        public bool HasCoreclrVersion { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(PathToVersionsFile))
            {
                Log.LogError($"File {PathToVersionsFile} does not exist.");
                return false;
            }
            string versionsFileContents = File.ReadAllText(PathToVersionsFile);
            CorefxHash = GetVersionHash("corefx", versionsFileContents);
            DotnetVersionsCorefxHash = GetVersionHash("dotnet/versions/corefx", versionsFileContents);
            CoreclrHash = GetVersionHash("coreclr", versionsFileContents);
            DotnetVersionsCoreclrHash = GetVersionHash("dotnet/versions/coreclr", versionsFileContents);
            CoresetupHash = GetVersionHash("core-setup", versionsFileContents);
            HasCoreclrVersion = !(string.IsNullOrEmpty(CoreclrHash));
            return true;
        }

        public string GetVersionHash(string repo, string fileContents)
        {
            var match = Regex.Match(fileContents, $"^{repo} (\\S.*)", RegexOptions.Multiline);
            return (match.Success && match.Groups.Count > 1) ? match.Groups[1].Value : string.Empty;
        }
    }
}
