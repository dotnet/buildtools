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
    public class UpdateProjectDependencies : Task
    {
        [Required]
        public ITaskItem ProjectJson { get; set; }

        public string OldPrerelease { get; set; }

        public string NewPrerelease { get; set; }

        public string PackageId { get; set; }

        public string OldVersion { get; set; }

        public string NewVersion { get; set; }

        public override bool Execute()
        {
            if (new[] { OldPrerelease, NewPrerelease }.All(s => !string.IsNullOrEmpty(s)))
            {
                ReplacePrerelease();
            }
            if (new[] { PackageId, OldVersion, NewVersion }.All(s => !string.IsNullOrEmpty(s)))
            {
                ReplacePackageVersion();
            }
            return !Log.HasLoggedErrors;
        }

        public void ReplacePrerelease()
        {
            Log.LogMessage(
                "Changing all packages with prerelease {0} to prerelease {1} in {2}",
                OldPrerelease,
                NewPrerelease,
                ProjectJson.ItemSpec);

            JObject projectRoot = ReadProject();
            bool changed = false;

            foreach (var package in projectRoot["dependencies"].OfType<JProperty>())
            {
                var dependencyIdentifier = package.Name;
                var dependencyVersionRange = VersionRange.Parse(package.Value.ToObject<string>());
                var dependencyVersion = dependencyVersionRange.MinVersion;

                if (dependencyVersion.Release == OldPrerelease)
                {
                    Log.LogMessage(
                        "Changing {0} {1}",
                        dependencyIdentifier,
                        dependencyVersion);

                    package.Value = dependencyVersionRange.OriginalString.Replace(OldPrerelease, NewPrerelease);
                    changed = true;
                }
            }
            if (changed)
            {
                WriteProject(projectRoot);
            }
        }

        public void ReplacePackageVersion()
        {
            Log.LogMessage(
                "Changing {0} version {1} to version {2} in {3}",
                PackageId,
                OldVersion,
                NewVersion,
                ProjectJson.ItemSpec);

            JObject projectRoot = ReadProject();
            bool changed = false;

            foreach (var package in projectRoot["dependencies"].OfType<JProperty>())
            {
                var dependencyIdentifier = package.Name;
                var dependencyVersion = package.Value.ToObject<string>();

                if (dependencyIdentifier == PackageId && dependencyVersion == OldVersion)
                {
                    Log.LogMessage(
                        "Changing {0} {1}",
                        dependencyIdentifier,
                        dependencyVersion);

                    package.Value = NewVersion;
                    changed = true;
                }
            }
            if (changed)
            {
                WriteProject(projectRoot);
            }
        }

        public JObject ReadProject()
        {
            using (TextReader projectFileReader = File.OpenText(ProjectJson.ItemSpec))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }

        public void WriteProject(JObject projectRoot)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);

            File.WriteAllText(ProjectJson.ItemSpec, projectJson + Environment.NewLine);
        }
    }
}
