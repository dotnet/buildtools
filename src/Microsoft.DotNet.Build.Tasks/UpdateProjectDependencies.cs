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
            var prereleaseUpdateParams = new[] { OldPrerelease, NewPrerelease }
                .Select(param => !string.IsNullOrEmpty(param));

            var versionUpdateParams = new[] { PackageId, OldVersion, NewVersion }
                .Select(param => !string.IsNullOrEmpty(param));

            bool anyPrereleaseUpdateParams = prereleaseUpdateParams.Any(p => p);
            bool anyVersionUpdateParams = versionUpdateParams.Any(p => p);

            if (anyPrereleaseUpdateParams ^ anyVersionUpdateParams)
            {
                if (prereleaseUpdateParams.All(p => p))
                {
                    ReplacePrerelease();
                }
                else if (versionUpdateParams.All(p => p))
                {
                    ReplacePackageVersion();
                }
                else
                {
                    if (anyPrereleaseUpdateParams)
                    {
                        Log.LogError("Not all properties found: expected (OldPrerelease, NewPrerelease)");
                    }
                    else
                    {
                        Log.LogError("Not all properties found: expected (PackageId, OldVersion, NewVersion)");
                    }
                }
            }
            else
            {
                Log.LogError(
                    "Expected properties from one of (OldPrerelease, NewPrerelease) or (PackageId, OldVersion, NewVersion) but found {0}.",
                    (anyPrereleaseUpdateParams && anyVersionUpdateParams) ? "properties from both" : "none");
            }
            return !Log.HasLoggedErrors;
        }

        public void ReplacePrerelease()
        {
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
                        "Changing {0} {1} to {2} in {3}",
                        dependencyIdentifier,
                        dependencyVersion,
                        NewPrerelease,
                        ProjectJson);

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
            JObject projectRoot = ReadProject();
            bool changed = false;

            foreach (var package in projectRoot["dependencies"].OfType<JProperty>())
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
                        ProjectJson);

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
