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
    /// </summary>
    public class AddDependenciesToProjectJson : Task
    {
        [Required]
        public string VersionStructureRegex { get; set; }

        // Additional Dependencies to add to the project.json. May Optionally contain a version.
        // Will Override dependencies present in the project if there is a conflict.
        // AdditionalDependencies required metadata:  Name, Version
        [Required]
        public ITaskItem[] AdditionalDependencies { get; set; }

        // Framework section which the additional dependencies apply to.  Empty is the default dependencies section.
        public string[] Frameworks { get; set; }

        public string BuildNumberOverrideStructureRegex { get; set; }

        // Permit overriding package versions found in project.json with custom build number version.
        public string PackageBuildNumberOverride { get; set; }

        // Original package version which is used to seed the output project.json
        [Required]
        public string ProjectJson { get; set; }

        // The directory to put the generated project.json in
        [Required]
        public string OutputProjectJson { get; set; }

        private Regex _versionStructureRegex;
        private Regex _buildNumberOverrideStructureRegex;

        public override bool Execute()
        {
            if (!File.Exists(ProjectJson))
            {
                Log.LogError("Cannot find specified project.json - '{0}'", ProjectJson);
                return false;
            }
            if (!string.IsNullOrWhiteSpace(PackageBuildNumberOverride))
            {
                if(string.IsNullOrWhiteSpace(BuildNumberOverrideStructureRegex))
                {
                    Log.LogError("Missing required parameter.  BuildNumberOverrideStructureRegex must be provided if PackageBuildNumberOverride is provided.");
                }
                _buildNumberOverrideStructureRegex = new Regex(BuildNumberOverrideStructureRegex);
                if(!_buildNumberOverrideStructureRegex.IsMatch(PackageBuildNumberOverride))
                {
                    Log.LogError("Invalid package version format: '{0}'", PackageBuildNumberOverride);
                    return false;
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
            _versionStructureRegex = new Regex(VersionStructureRegex);

            // No PackageBuildNumberOverride was specified, so try to find one to associate with our AdditionalDependencies
            PackageBuildNumberOverride = PackageBuildNumberOverride ?? DeriveBuildNumber(projectRoot);

            // No Frameworks were specified, apply AdditionalDependencies to all framework groups in the project.json
            if (Frameworks == null || Frameworks.Length == 0)
            {
                Frameworks = projectRoot.SelectTokens("frameworks").SelectMany(f => f.Children().Select(c => ((JProperty)c).Name)).ToArray();
            }

            // Update default dependencies section
            JObject dependencies = GenerateDependencies(projectRoot);
            projectRoot = UpdateDependenciesProperty(projectRoot, dependencies);

            // Update framework dependencies sections
            for (int i = 0; i < Frameworks.Length; i++)
            {
                dependencies = GenerateDependencies(projectRoot, Frameworks[i]);
                projectRoot = UpdateDependenciesProperty(projectRoot, dependencies, Frameworks[i]);
            }
            WriteProject(projectRoot, OutputProjectJson);

            return true;
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
                var _frameworkPath = "frameworks." + Frameworks[i];
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
            return projectJsonRoot.SelectToken("frameworks." + framework + ".dependencies");
        }

        // Generate the combines dependencies from the projectjson jObject and from AdditionalDependencies
        private JObject GenerateDependencies(JObject projectJsonRoot, string framework = null)
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
                    NuGetVersion nuGetVersion;
                    if (NuGetVersion.TryParse(property.Value.ToString(), out nuGetVersion))
                    {
                        Match m = _versionStructureRegex.Match(nuGetVersion.ToString());

                        if (m.Success)
                        {
                            NuGetVersion dependencyVersion = nuGetVersion;
                            nuGetVersion = NuGetVersion.Parse(string.Join(".", dependencyVersion.Major, dependencyVersion.Minor, dependencyVersion.Patch) + "-" + PackageBuildNumberOverride);
                        }
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
            }

            foreach (var dependency in AdditionalDependencies)
            {
                string name = dependency.GetMetadata("Name");
                // Don't add a new dependency if one already exists.
                if (returnDependenciesList.FirstOrDefault(rd => ((JProperty)rd).Name.Equals(name)) == null)
                {
                    NuGetVersion dependencyVersion = NuGetVersion.Parse(dependency.GetMetadata("Version"));

                    string version = string.Join(".", dependencyVersion.Major, dependencyVersion.Minor, dependencyVersion.Patch);
                    if (!string.IsNullOrWhiteSpace(PackageBuildNumberOverride))
                    {
                        version += "-" + PackageBuildNumberOverride;
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

        /* No build number was specified, determine the build number by examining the other packages in the dependencies list */
        private string DeriveBuildNumber(JObject projectRoot)
        {
            var dependenciesList = projectRoot
                .DescendantsAndSelf()
                .OfType<JProperty>()
                .Where(property => property.Name == "dependencies")
                .Select(property => property.Value)
                .SelectMany(o => o.Children<JProperty>());

            foreach (JProperty property in dependenciesList.Select(dl => (JProperty)dl))
            {
                string version = property.Value.ToString();
                Match m = _versionStructureRegex.Match(version);
                if (m.Success)
                {
                    string buildNumber = m.Groups[2].Value;
                    Log.LogMessage("Determined buildnumber using existing package dependencies as '{0}'", buildNumber);
                    return buildNumber;
                }
            }
            return PackageBuildNumberOverride;
        }

        /* Given a project.json as a JObject, replace it's dependencies property with a new dependencies property. */
        private JObject UpdateDependenciesProperty(JObject projectJsonRoot, JObject updatedProperties, string framework = null)
        {
            var frameworkPath = string.Empty;
            if(!string.IsNullOrWhiteSpace(framework))
            {
                frameworkPath = "frameworks." + framework;
            }
            var frameworkPathObject = projectJsonRoot.SelectToken(frameworkPath);
            frameworkPathObject["dependencies"] = updatedProperties;
            return projectJsonRoot;
        }

        private static void WriteProject(JObject projectRoot, string projectJsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(projectJsonPath));
            File.WriteAllText(projectJsonPath, projectJson + Environment.NewLine); 
        }
    }
}
