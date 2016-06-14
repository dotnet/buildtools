using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Parse a project.json, and add additional dependencies, then write a out new project.json.
    /// Use-case scenarios
    /// 1. Provide a list of package drops, this becomes the source of package versions
    /// 2. Provide a versions files, this becomes the source of package versions
    /// If both a package drop and a version file are provided, then the package drop takes precedent over the version file.
    /// </summary>
    public class AddDependenciesToProjectJson : Task
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
        public string DownloadFileLocation { get; set; }

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

        private Regex _packageNameRegex;

        public override bool Execute()
        {
            if (!File.Exists(ProjectJson))
            {
                Log.LogError("Cannot find specified project.json - '{0}'", ProjectJson);
                return false;
            }

            List<ITaskItem> packageInformation = new List<ITaskItem>();
            _packageNameRegex = new Regex(PackageNameRegex);

            // Retrieve package information from a package drop location
            if (PackagesDrops != null &&
                PackagesDrops.Length > 0)
            {
                packageInformation.AddRange(GatherPackageInformationFromDrops(PackagesDrops));
            }

            // Retrieve package information from a versions file
            if (VersionsFiles != null)
            {
                foreach (var versionsFile in VersionsFiles)
                {
                    if (!File.Exists(versionsFile))
                    {
                        Log.LogError("Version file {0} does not exist.", versionsFile);
                    }

                    packageInformation.AddRange(GatherPackageInformationFromVersionsFile(versionsFile));
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
            WriteProject(projectRoot, OutputProjectJson);

            return true;
        }

        /// <summary>
        /// Given a package name regex pattern, and an array of drop locations, create an array of objects
        /// containing package information (name, version,, prerelease version)
        /// </summary>
        /// <param name="packagesDrops"></param>
        /// <returns></returns>
        private IEnumerable<ITaskItem> GatherPackageInformationFromDrops(string [] packagesDrops)
        {
            List<ITaskItem> packageNameItems = new List<ITaskItem>();

            foreach (string packageDrop in packagesDrops)
            {
                if (!Directory.Exists(packageDrop))
                {
                    Log.LogWarning("PackageDrop does not exist - '{0}'", packageDrop);
                    continue;
                }
                IEnumerable<ITaskItem> packages = Directory.GetFiles(packageDrop).Select(f => new TaskItem(Path.GetFileNameWithoutExtension(f)));

                foreach (ITaskItem package in packages)
                {
                    packageNameItems.Add(CreatePackageItemFromString(package.ItemSpec));
                }
            }
            return packageNameItems;
        }

        // A versions file is of the form https://github.com/dotnet/versions/blob/master/build-info/dotnet/corefx/master/Latest_Packages.txt
        private IEnumerable<ITaskItem> GatherPackageInformationFromVersionsFile(string versionsFile)
        {
            List<ITaskItem> packageNameItems = new List<ITaskItem>();
            if(!File.Exists(versionsFile))
            {
                Log.LogError("Specified versions file ({0}) does not exist.", versionsFile);
            }
            var lines = File.ReadAllLines(versionsFile);
            foreach(string line in lines)
            { 
                if(!string.IsNullOrWhiteSpace(line))
                {
                    string packageVersion = line.Replace(' ', '.');
                    packageNameItems.Add(CreatePackageItemFromString(packageVersion));
                }
            }
            return packageNameItems;
        }

        private TaskItem CreatePackageItemFromString(string package)
        {
            Match m = _packageNameRegex.Match(package);
            TaskItem packageItem = null;
            if (m.Success)
            {
                packageItem = new TaskItem(m.Groups[0].Value);
                string name = m.Groups[1].Value;
                packageItem.SetMetadata("Name", name);
                packageItem.SetMetadata("Version", m.Groups[2].Value);
                packageItem.SetMetadata("ReleaseVersion", m.Groups[3].Value);
            }
            return packageItem;
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

        private JToken GetFrameworkDependenciesSection(JObject projectJsonRoot, string framework = null)
        {
            if(string.IsNullOrWhiteSpace(framework))
            {
                return projectJsonRoot["dependencies"];
            }
            return projectJsonRoot.SelectToken("frameworks." + NewtonsoftEscapeJProperty(framework) + ".dependencies");
        }

        // Generate the combines dependencies from the projectjson jObject and from AdditionalDependencies
        private JObject GenerateDependencies(JObject projectJsonRoot, ITaskItem[] externalPackageVersions, IEnumerable<ITaskItem> packageInformation, string framework = null)
        {
            var originalDependenciesList = new List<JToken>();
            var returnDependenciesList = new List<JToken>();
            var frameworkDependencies = GetFrameworkDependenciesSection(projectJsonRoot, framework);
            if (frameworkDependencies != null)
            {
                originalDependenciesList = frameworkDependencies.Children().ToList();

                // Update versions in dependencies
                foreach (JProperty property in originalDependenciesList.Select(od => od))
                {
                    ITaskItem package = GetPackageInformation(property.Name, packageInformation);

                    if (package != null)
                    {
                        NuGetVersion nuGetVersion;
                        if (NuGetVersion.TryParse(property.Value.ToString(), out nuGetVersion))
                        {
                            NuGetVersion dependencyVersion = nuGetVersion;
                            string ver = null;

                            // a package version was provided, use its version information.
                            if (package != null)
                            {
                                ver = package.GetMetadata("Version");
                            }
                            else
                            {
                                ver = string.Join(".", dependencyVersion.Major, dependencyVersion.Minor, dependencyVersion.Patch);
                            }

                            string releaseVersion = package.GetMetadata("ReleaseVersion");
                            // we have package information, so use that.
                            if (!string.IsNullOrWhiteSpace(releaseVersion))
                            {
                                ver = ver + "-" + releaseVersion;
                            }
                            nuGetVersion = NuGetVersion.Parse(ver);
                        }

                        // Only add the original dependency if it wasn't passed as an AdditionalDependency, ie. AdditionalDependencies may override dependencies in project.json
                        if (AdditionalDependencies.FirstOrDefault(d => d.GetMetadata("Name").Equals(property.Name, StringComparison.OrdinalIgnoreCase)) == null)
                        {
                            JProperty addProperty;
                            if (nuGetVersion != null)
                            {
                                addProperty = new JProperty(property.Name, nuGetVersion.ToString());
                            }
                            else
                            {
                                addProperty = property;
                            }
                            returnDependenciesList.Add(addProperty);
                        }
                    }
                    else
                    {
                        returnDependenciesList.Add(property);
                    }
                }
            }

            foreach (var dependency in AdditionalDependencies)
            {
                string name = dependency.GetMetadata("Name");
                // Don't add a new dependency if one already exists.
                if (returnDependenciesList.FirstOrDefault(rd => ((JProperty)rd).Name.Equals(name)) == null)
                {
                    ITaskItem package = GetPackageInformation(name, packageInformation);
                    string version = null;

                    NuGetVersion dependencyVersion = NuGetVersion.Parse(dependency.GetMetadata("Version"));
                    version = string.Join(".", dependencyVersion.Major, dependencyVersion.Minor, dependencyVersion.Patch);

                    // a package version was provided, use its version information.
                    if (package != null)
                    {
                        version = package.GetMetadata("Version");
                        string releaseVersion = package.GetMetadata("ReleaseVersion");
                        if (!string.IsNullOrWhiteSpace(releaseVersion))
                        {
                            version += "-" + releaseVersion;
                        }
                    }
                    else if(!string.IsNullOrWhiteSpace(dependency.GetMetadata("PackageVersion")))
                    {
                        version += "-" + dependency.GetMetadata("PackageVersion");
                    }
                    JProperty property = new JProperty(name, version);
                    returnDependenciesList.Add(property);
                }
                else
                {
                    Log.LogMessage("Ignoring AdditionalDependency '{0}', dependency is already present in {1}", name, ProjectJson);
                }
            }
            
            return new JObject(returnDependenciesList.ToArray());
        }

        private ITaskItem GetPackageInformation(string name, IEnumerable<ITaskItem> packageInformation)
        {
            foreach(var package in packageInformation)
            {
                if(name.Equals(package.GetMetadata("Name"), StringComparison.OrdinalIgnoreCase))
                {
                    return package;
                }
            }
            return null;
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
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(projectJsonPath));
            File.WriteAllText(projectJsonPath, projectJson + Environment.NewLine); 
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
}
