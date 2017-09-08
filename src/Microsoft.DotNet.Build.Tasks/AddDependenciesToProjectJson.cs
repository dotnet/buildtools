// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Net.Http;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Parse a project.json, and add additional dependencies, then write a out new project.json.
    /// Use-case scenarios
    /// 1. Provide a list of package drops, this becomes the source of package versions
    /// 2. Provide a versions files, this becomes the source of package versions
    /// If both a package drop and a version file are provided, then the package drop takes precedent over the version file.
    /// </summary>
    public class AddDependenciesToProjectJson : BuildTask
    {
        // Additional Dependencies to add to the project.json. May Optionally contain a version.
        // Will Override dependencies present in the project if there is a conflict.
        // AdditionalDependencies required metadata:  Name, Version
        [Required]
        public ITaskItem[] AdditionalDependencies { get; set; }

        // Framework section which the additional dependencies apply to.  Empty is the default dependencies section.
        public string[] Frameworks { get; set; }

        public string[] PackagesDrops { get; set; }

        [Required]
        public string PackageNameRegex { get; set; }

        public string[] VersionsFiles { get; set; }

        /// <summary>
        /// If there are multiple package items from different sources (ie, package items found in one or more package drops,
        /// package items found in one or more versions files) with the same package name, allow the conflict, but choose
        /// the newest package version.
        /// </summary>
        public bool UseNewestAvailablePackages { get; set; }

        /// <summary>
        /// Original package version which is used to seed the output project.json
        /// </summary>
        [Required]
        public string ProjectJson { get; set; }

        /// <summary>
        /// External package dependency versions.
        /// </summary>
        public ITaskItem[] ExternalPackages { get; set; }

        /// <summary>
        /// Optional list of RIDs to exclude from the generated project.json.
        /// </summary>
        public string[] ExcludedRuntimes { get; set; }

        // The directory to put the generated project.json in
        [Required]
        public string OutputProjectJson { get; set; }
        
        public string SupportsFile { get; set; }

        public string TestRuntime { get; set; }

        public string TestTargetFramework { get; set; }

        private Regex _packageNameRegex;

        private VersionComparer comparer = new VersionComparer(VersionComparison.VersionRelease);

        public override bool Execute()
        {
            if (!File.Exists(ProjectJson))
            {
                Log.LogError("Cannot find specified project.json - '{0}'", ProjectJson);
                return false;
            }

            Dictionary<string, PackageItem> packageInformation = new Dictionary<string, PackageItem>();
            _packageNameRegex = new Regex(PackageNameRegex);

            // Retrieve package information from a package drop location
            if (PackagesDrops != null &&
                PackagesDrops.Length > 0)
            {
                AddPackageItemsToDictionary(ref packageInformation, GatherPackageInformationFromDrops(PackagesDrops));
            }

            // Retrieve package information from a versions file
            if (VersionsFiles != null)
            {
                foreach (var versionsUri in VersionsFiles.Select(v => new Uri(v)))
                {
                    AddPackageItemsToDictionary(ref packageInformation, GatherPackageInformationFromVersionsFile(versionsUri, comparer));
                }
            }

            JObject projectRoot = ReadProject(ProjectJson);
            var invalidFramework = AreValidFrameworkPaths(projectRoot);
            if(invalidFramework != string.Empty)
            {
                OutputProjectJson = ProjectJson;
                Log.LogError("Unable to find framework section '{0}' in '{1}'", invalidFramework, ProjectJson);
                return false;
            }

            // No Frameworks were specified, apply AdditionalDependencies to all framework groups in the project.json
            if (Frameworks == null || Frameworks.Length == 0)
            {
                Frameworks = projectRoot.SelectTokens("frameworks").SelectMany(f => f.Children().Select(c => ((JProperty)c).Name)).ToArray();
            }

            // Update default dependencies section
            JObject dependencies = GenerateDependencies(projectRoot, ExternalPackages, packageInformation);
            projectRoot = UpdateDependenciesProperty(projectRoot, dependencies);

            if (ExcludedRuntimes != null)
            {
                var excludedRIDs = new HashSet<string>(ExcludedRuntimes, StringComparer.OrdinalIgnoreCase);
                projectRoot = FilterRuntimes(projectRoot, excludedRIDs);
            }

            // Update framework dependencies sections
            for (int i = 0; i < Frameworks.Length; i++)
            {
                dependencies = GenerateDependencies(projectRoot, ExternalPackages, packageInformation, Frameworks[i]);
                projectRoot = UpdateDependenciesProperty(projectRoot, dependencies, Frameworks[i]);
            }

            if (SupportsFile != null)
            {
                var definedTfmRidPairs = FilterRuntimesFromSupports.GetAllTfmRidPairs(SupportsFile);
                var applicableTfmRidPairs = FilterRuntimesFromSupports.FilterForApplicableTFMRIDPairs(
                    definedTfmRidPairs, TestTargetFramework, TestRuntime);
                projectRoot["supports"] = FilterRuntimesFromSupports.GenerateCustomSupportsClause(applicableTfmRidPairs);
            }
            WriteProject(projectRoot, OutputProjectJson);

            return true;
        }

        /// <summary>
        /// Given a package name regex pattern, and an array of drop locations, create an array of objects
        /// containing package information (name, version,, prerelease version)
        /// </summary>
        /// <param name="packagesDrops"></param>
        /// <returns></returns>
        private Dictionary<string, PackageItem> GatherPackageInformationFromDrops(string [] packagesDrops)
        {
            Dictionary<string, PackageItem> packageItems = new Dictionary<string, PackageItem>();

            foreach (string packageDrop in packagesDrops)
            {
                if (!Directory.Exists(packageDrop))
                {
                    Log.LogWarning("PackageDrop does not exist - '{0}'", packageDrop);
                    continue;
                }
                IEnumerable<string> packages = Directory.GetFiles(packageDrop);

                foreach (var package in packages)
                {
                    PackageItem packageItem = CreatePackageItem(package);

                    AddPackageItemToDictionary(packageItems, packageItem);
                }
            }
            return packageItems;
        }

        private void AddPackageItemToDictionary(Dictionary<string, PackageItem> packageItems, PackageItem packageItem)
        {
            if (packageItems.ContainsKey(packageItem.Name))
            {
                if (comparer == null)
                {
                    comparer = new VersionComparer(VersionComparison.VersionRelease);
                }
                if (comparer.Compare(packageItems[packageItem.Name].Version, packageItem.Version) != 0 && UseNewestAvailablePackages != true)
                {
                    Log.LogError("Package named {0} already exists.  Cannot have multiple packages with the same name.\n", packageItem.Name);
                    Log.LogError("To permit package name clashes and take latest, specify 'UseNewestAvailablePackages=true'.\n");
                    Log.LogError("Package {0} version {1} clashes with {2}", packageItem.Name, packageItems[packageItem.Name].Version.ToFullString(), packageItem.Version.ToFullString());
                }
                else if (UseNewestAvailablePackages == true)
                {
                    PackageItem item = (comparer.Compare(packageItems[packageItem.Name].Version, packageItem.Version) < 0) ? packageItem : packageItems[packageItem.Name];
                    packageItems[packageItem.Name] = item;
                }
            }
            else
            {
                packageItems.Add(packageItem.Name, packageItem);
            }
        }
        private void AddPackageItemsToDictionary(ref Dictionary<string, PackageItem> packageItems, Dictionary<string, PackageItem> addPackageItems)
        {
            foreach(var packageItem in addPackageItems.Values)
            {
                AddPackageItemToDictionary(packageItems, packageItem);
            }
        }

        private static async System.Threading.Tasks.Task<Stream> GetStream(Uri uri)
        {
            if(uri.Scheme == "file")
            {
                return new FileStream(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            else
            {
                return await new HttpClient().GetStreamAsync(uri);
            }
        }

        // A versions file is of the form https://raw.githubusercontent.com/dotnet/versions/master/build-info/dotnet/corefx/release/1.0.0/LKG_Packages.txt
        private Dictionary<string, PackageItem> GatherPackageInformationFromVersionsFile(Uri uri, VersionComparer comparer = null)
        {
            Dictionary<string, PackageItem> packageItems = new Dictionary<string, PackageItem>();

            try
            {
                using (var streamReader = new StreamReader(GetStream(uri).Result))
                {
                    while (!streamReader.EndOfStream)
                    {
                        string line = streamReader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            string[] packageVersionTokens = line.Split(' ');
                            PackageItem packageItem = CreatePackageItem(packageVersionTokens[0], packageVersionTokens[1]);
                            AddPackageItemToDictionary(packageItems, packageItem);
                        }
                    }
                }
            }
            catch (AggregateException)
            {
                Log.LogError("Error: Unable to open '{0}', either the file does not exist locally or there is a network issue accessing that URI.", uri.ToString());
                throw;
            }
            return packageItems;
        }

        /// <summary>
        /// Create a package item object from a nupkg file
        /// </summary>
        /// <param name="package">path to a nupkg</param>
        /// <returns></returns>
        private PackageItem CreatePackageItem(string package)
        {
            using (PackageArchiveReader archiveReader = new PackageArchiveReader(package))
            {
                PackageIdentity identity = archiveReader.GetIdentity();
                return new PackageItem(identity.Id, identity.Version);
            }
        }

        /// <summary>
        /// Create a package item object from a package name (id) and version
        /// </summary>
        /// <param name="id"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        private PackageItem CreatePackageItem(string id, string version)
        {
            NuGetVersion nuGetVersion = new NuGetVersion(version);
            return new PackageItem(id, nuGetVersion);
        }

        private string AreValidFrameworkPaths(JObject projectRoot)
        {
            if(Frameworks == null ||
                Frameworks.Length == 0)
            {
                return string.Empty;
            }
            // Check for a valid path, if invalid, exit
            for (int i = 0; i < Frameworks.Length; i++)
            {
                var _frameworkPath = "frameworks." + NewtonsoftEscapeJProperty(Frameworks[i]);
                var validFramework = projectRoot.SelectToken(_frameworkPath);
                if (validFramework == null)
                {
                    return _frameworkPath;
                }
            }
            return string.Empty;
        }

        private static JObject ReadProject(string projectJsonPath)
        { 
           using (TextReader projectFileReader = File.OpenText(projectJsonPath)) 
           { 
              var projectJsonReader = new JsonTextReader(projectFileReader); 
              var serializer = new JsonSerializer(); 
              return serializer.Deserialize<JObject>(projectJsonReader); 
           } 
        }

        private JObject GetFrameworkDependenciesSection(JObject projectJsonRoot, string framework = null)
        {
            if(string.IsNullOrWhiteSpace(framework))
            {
                return (JObject) projectJsonRoot["dependencies"];
            }
            return (JObject) projectJsonRoot["frameworks"][framework]["dependencies"];
        }

        // Generate the combines dependencies from the projectjson jObject and from AdditionalDependencies
        private JObject GenerateDependencies(JObject projectJsonRoot, ITaskItem[] externalPackageVersions, Dictionary<string, PackageItem> packageInformation, string framework = null)
        {
            var originalDependenciesList = new List<JToken>();
            var returnDependenciesList = new Dictionary<string, JToken>();
            var frameworkDependencies = GetFrameworkDependenciesSection(projectJsonRoot, framework);

            if (frameworkDependencies != null)
            {
                originalDependenciesList = frameworkDependencies.Children().ToList();

                // Update versions in dependencies
                foreach (JProperty property in originalDependenciesList.Select(od => od))
                {
                    PackageItem packageItem = null;
                    if (packageInformation.ContainsKey(property.Name))
                    {
                        packageItem = packageInformation[property.Name];

                        NuGetVersion nuGetVersion = packageItem.Version;

                        // Only add the original dependency if it wasn't passed as an AdditionalDependency, ie. AdditionalDependencies may override dependencies in project.json
                        if (!AdditionalDependencies.Any(d => d.ItemSpec.Equals(property.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            JProperty addProperty;
                            if (nuGetVersion != null)
                            {
                                //If the dependency is of the form 
                                // "Dependency" : {
                                // "version": "3.1.4-beta-23456-00",
                                // "include": "compile"
                                //  }
                                if (property.Value.Type==JTokenType.Object)
                                {
                                    JObject jo = property.Value as JObject;

                                    if (jo["version"] != null)
                                    {
                                        jo.Remove("version");
                                        jo["version"] = nuGetVersion.ToString();
                                    }
                                    else
                                    {
                                        Log.LogMessage("Ignoring Dependency '{0}', property does not have a version", property.Name);
                                    }
                                    addProperty = new JProperty(property.Name, jo);
                                }
                                else
                                {
                                    addProperty = new JProperty(property.Name, nuGetVersion.ToString());
                                }
                            }
                            else
                            {
                                addProperty = property;
                            }
                            returnDependenciesList.Add(property.Name, addProperty);
                        }
                    }
                    else
                    {
                        returnDependenciesList.Add(property.Name, property);
                    }
                }
            }

            foreach (var dependency in AdditionalDependencies)
            {
                string name = dependency.GetMetadata("Name");
                // Don't add a new dependency if one already exists.
                if (!returnDependenciesList.ContainsKey(name))
                {
                    NuGetVersion nuGetVersion = NuGetVersion.Parse(dependency.GetMetadata("Version"));
                    PackageItem packageItem = new PackageItem(name, nuGetVersion);
                    string version = packageItem.GetVersionString();

                    // a package version was provided, use its version information.
                    if (packageInformation.ContainsKey(name))
                    {
                        version = packageInformation[name].Version.ToString();
                    }
                    JProperty property = new JProperty(name, version);
                    returnDependenciesList.Add(name, property);
                }
                else
                {
                    Log.LogMessage("Ignoring AdditionalDependency '{0}', dependency is already present in {1}", name, ProjectJson);
                }
            }

            return new JObject(returnDependenciesList.Values.ToArray());
        }

        /* Given a project.json as a JObject, replace it's dependencies property with a new dependencies property. */
        private JObject UpdateDependenciesProperty(JObject projectJsonRoot, JObject updatedProperties, string framework = null)
        {
            var frameworkPath = string.Empty;
            if(!string.IsNullOrWhiteSpace(framework))
            {
                frameworkPath = "frameworks." + NewtonsoftEscapeJProperty(framework);
            }
            var frameworkPathObject = projectJsonRoot.SelectToken(frameworkPath);
            frameworkPathObject["dependencies"] = updatedProperties;
            return projectJsonRoot;
        }

        private JObject FilterRuntimes(JObject projectRoot, HashSet<string> excludedRIDs)
        {
            var runtimes = projectRoot["runtimes"];
            if (runtimes != null)
            {
                var toRemove = new List<JToken>();
                foreach (JProperty runtime in runtimes)
                {
                    if (excludedRIDs.Contains(runtime.Name))
                        toRemove.Add(runtime);
                }

                foreach (var token in toRemove)
                {
                    Log.LogMessage("Removing RID '{0}' from the list of applicable runtimes.", ((JProperty)token).Name);
                    token.Remove();
                }
            }

            return projectRoot;
        }

        private static void WriteProject(JObject projectRoot, string projectJsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented) + Environment.NewLine;

            if (!File.Exists(projectJsonPath) || !projectJson.Equals(File.ReadAllText(projectJsonPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(projectJsonPath));
                File.WriteAllText(projectJsonPath, projectJson);
            }
        }

        /* JProperties are encapsulated with "['" and "']" to assist with matching Paths which
           contain properties with a '.'.  ie. frameworks.netcoreapp1.0 becomes frameworks.['netcoreapp1.0'].
           A match for a property without a '.' and unencapsulated still works.  ie, we can still select
           frameworks.['netcoreapp1.0'] even if internally its path is frameworks.netcoreapp1.0. */
        private static string NewtonsoftEscapeJProperty(string property)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                return property;
            }
            if (!property.StartsWith("['") && !property.EndsWith("']"))
            {
                property = "['" + property + "']";
            }
            return property;
        }
    }

    internal class PackageItem
    {
        public string Name { get; set; }
        public NuGetVersion Version
        {
            set { _version = value; }
            get { return _version; }
        }

        NuGetVersion _version;

        public PackageItem() { }
        public PackageItem(string name) { Name = name; }
        public PackageItem(string name, NuGetVersion version) { Name = name;  Version = version; }

        public string GetVersionString()
        {
            return string.Join(".", _version.Major, _version.Minor, _version.Patch);
        }

        public TaskItem ToTaskItem()
        {
            TaskItem taskItem = new TaskItem(Name);
            taskItem.SetMetadata("Name", Name);
            taskItem.SetMetadata("Version", string.Join(".", Version.Major, Version.Minor, Version.Patch));
            taskItem.SetMetadata("Prerelease", Version.Release);
            return taskItem;
        }
    }
}
