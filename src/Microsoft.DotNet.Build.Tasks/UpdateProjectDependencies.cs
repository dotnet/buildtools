using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public abstract class UpdateProjectDependencies : Task
    {
        [Required]
        public ITaskItem[] ProjectJsons { get; set; }

        private static JObject ReadProject(string projectJsonPath)
        {
            using (TextReader projectFileReader = File.OpenText(projectJsonPath))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }

        private static void WriteProject(JObject projectRoot, string projectJsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);

            File.WriteAllText(projectJsonPath, projectJson + Environment.NewLine);
        }

        public abstract bool UpdatePackage(JProperty package, string projectJsonPath);

        public override bool Execute()
        {
            foreach (var projectJsonPath in ProjectJsons.Select(item => item.ItemSpec))
            {
                JObject projectRoot = ReadProject(projectJsonPath);

                bool changedAnyPackage = projectRoot["dependencies"]
                    .OfType<JProperty>()
                    .Any(package => UpdatePackage(package, projectJsonPath));

                if (changedAnyPackage)
                {
                    WriteProject(projectRoot, projectJsonPath);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }

    public class UpdatePrereleaseDependencies : UpdateProjectDependencies
    {
        [Required]
        public string OldPrerelease { get; set; }

        [Required]
        public string NewPrerelease { get; set; }

        public override bool UpdatePackage(JProperty package, string projectJsonPath)
        {
            var dependencyIdentifier = package.Name;
            var dependencyVersionRange = VersionRange.Parse(package.Value.ToObject<string>());
            var dependencyVersion = dependencyVersionRange.MinVersion;

            if (dependencyVersion.Release == OldPrerelease)
            {
                Log.LogMessage(
                    "Changing {0} {1} to {2} in {3}",
                    dependencyIdentifier,
                    dependencyVersion,
                    NewPrerelease,
                    projectJsonPath);

                package.Value = dependencyVersionRange.OriginalString.Replace(OldPrerelease, NewPrerelease);
                return true;
            }
            return false;
        }
    }

    public class UpdatePackageDependencyVersion : UpdateProjectDependencies
    {
        [Required]
        public string PackageId { get; set; }

        [Required]
        public string OldVersion { get; set; }

        [Required]
        public string NewVersion { get; set; }

        public override bool UpdatePackage(JProperty package, string projectJsonPath)
        {
            var dependencyIdentifier = package.Name;
            var dependencyVersion = package.Value.ToObject<string>();

            if (dependencyIdentifier == PackageId && dependencyVersion == OldVersion)
            {
                Log.LogMessage(
                    "Changing {0} {1} to {2} in {3}",
                    dependencyIdentifier,
                    dependencyVersion,
                    NewVersion,
                    projectJsonPath);

                package.Value = NewVersion;
                return true;
            }
            return false;
        }
    }
}
