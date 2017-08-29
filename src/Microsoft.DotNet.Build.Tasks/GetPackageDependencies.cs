// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetPackageDependencies : BuildTask
    {
        /// <summary>
        /// An ItemGroup of full paths to packages.config NuGet files.
        /// </summary>
        [Required]
        public ITaskItem[] PackageConfigs { get; set; }

        /// <summary>
        /// An ItemGroup of package dependencies.  The ItemSpec is the name of the package.
        /// A metadatum named RequestedVersion is which version is specified in the original packages.config
        /// </summary>
        [Output]
        public ITaskItem[] PackageDependencies { get; set; }

        public override bool Execute()
        {
            Log.LogMessage("Starting GetPackageDependencies");

            // Aggregate package dependencies in to local packageDependencies variable
            List<Tuple<string, string>> packageDependencies = new List<Tuple<string, string>>();
            foreach (string packageConfigPath in PackageConfigs.Select(pc => pc.ItemSpec))
            {
                if (!File.Exists(packageConfigPath))
                {
                    throw new InvalidOperationException(string.Format(
                        "GetPackageDependencies was given a package config file that does not exist: \"{0}\"", packageConfigPath));
                }

                GetDependencies(packageConfigPath, (string name, string version) =>
                    {
                        Tuple<string, string> packageDependency = Tuple.Create(name, version);

                        if (!packageDependencies.Contains(packageDependency))
                        {
                            packageDependencies.Add(packageDependency);
                        }
                    });
            }

            // Transform local package dependencies representation to MSBuild's ITaskItem[]
            PackageDependencies = packageDependencies.Select(
                pd => new TaskItem(pd.Item1, new Dictionary<string, string>() { { "RequestedVersion", pd.Item2 } })
                ).ToArray();

            Log.LogMessage(string.Format(
                "GetPackageDependencies completed successfully - discovered {0} package {1} in {2} packages.config {3}.",
                PackageDependencies.Count(),
                PackageDependencies.Count() == 1 ? "dependency" : "dependencies",
                PackageConfigs.Count(),
                PackageConfigs.Count() == 1 ? "file" : "files"));

            return true;
        }

        private void GetDependencies(string packageConfigPath, Action<string, string> onFoundDependency)
        {
            Action<string> parseError = (message) =>
                {
                    throw new InvalidOperationException(string.Format(
                        "Could not parse packages.config file \"{0}\"\n{1}", packageConfigPath, message));
                };


            Func<XElement, string, string> getSingleAttributeValue = (elem, name) =>
                {
                    if (elem.Attributes(name).Count() == 0)
                    {
                        parseError(string.Format("Expected, but did not find an \"{0}\" attribute on a \"package\" element.", name));
                    }

                    if (elem.Attributes(name).Count() > 1)
                    {
                        parseError(string.Format("Found duplicate \"{0}\" attributes on a \"package\" element, expected one.", name));
                    }

                    return elem.Attributes(name).Single().Value;
                };

            XDocument xdoc = XDocument.Load(packageConfigPath);
            if (xdoc.Root.Name != "packages")
            {
                parseError(string.Format("Expected the root element to be \"packages\", got \"{0}\"", xdoc.Root.Name));
            }

            foreach (XElement elem in xdoc.Root.Descendants("package"))
            {
                string name = getSingleAttributeValue(elem, "id");
                string version = getSingleAttributeValue(elem, "version");

                onFoundDependency(name, version);
            }
        }
    }
}
