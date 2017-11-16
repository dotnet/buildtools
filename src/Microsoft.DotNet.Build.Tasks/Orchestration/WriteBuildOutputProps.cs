// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.Orchestration
{
    public class WriteBuildOutputProps : BuildTask
    {
        private const string NuGetPackageInfoId = "PackageId";
        private const string NuGetPackageInfoVersion = "PackageVersion";

        /// <summary>
        /// Group 1: matches the beginning of the package id or any non-alphanumeric character.
        /// Group 2 (FirstPartChar): matches one character after group 1.
        /// 
        /// By replacing every match with FirstPartChar.ToUpper, non-alphanumeric separators such as
        /// '.' and '_' are discarded and the package name is converted to PascalCase.
        /// </summary>
        private static Regex s_packageNamePascalCasingRegex =
            new Regex(@"(^|[^A-Za-z0-9])(?<FirstPartChar>.)");

        [Required]
        public ITaskItem[] NuGetPackageInfos { get; set; }

        [Required]
        public string OutputPath { get; set; }

        /// <summary>
        /// Prevents overriding stable packages. Adds a condition to each package version property:
        /// only set the property when the property already exists and specifies a prerelease.
        /// </summary>
        public bool OnlyUpdatePrereleaseVersions { get; set; }

        public override bool Execute()
        {
            PackageIdentity[] latestPackages = NuGetPackageInfos
                .Select(item =>
                {
                    var id = item.GetMetadata(NuGetPackageInfoId);
                    var version = item.GetMetadata(NuGetPackageInfoVersion);

                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                    {
                        return null;
                    }

                    return new PackageIdentity(id, NuGetVersion.Parse(version));
                })
                .Where(identity => identity != null)
                .GroupBy(identity => identity.Id)
                .Select(g => g.OrderBy(id => id.Version).Last())
                .OrderBy(id => id.Id)
                .ToArray();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            using (var outStream = File.Open(OutputPath, FileMode.Create))
            using (var sw = new StreamWriter(outStream, new UTF8Encoding(false)))
            {
                sw.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
                sw.WriteLine(@"<Project ToolsVersion=""14.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">");
                sw.WriteLine(@"  <PropertyGroup>");
                foreach (PackageIdentity packageIdentity in latestPackages)
                {
                    string formattedId = s_packageNamePascalCasingRegex.Replace(
                        packageIdentity.Id,
                        match => match.Groups?["FirstPartChar"].Value.ToUpperInvariant()
                            ?? string.Empty);

                    string propertyName = $"{formattedId}PackageVersion";

                    string condition = string.Empty;
                    if (OnlyUpdatePrereleaseVersions)
                    {
                        condition = $@" Condition=""$({propertyName}.Contains('-'))""";
                    }

                    sw.WriteLine($"    <{propertyName}{condition}>{packageIdentity.Version}</{propertyName}>");
                }
                sw.WriteLine(@"  </PropertyGroup>");
                sw.WriteLine(@"</Project>");
            }

            return true;
        }
    }
}
