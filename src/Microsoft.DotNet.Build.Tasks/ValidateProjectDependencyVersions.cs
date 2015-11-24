using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ValidateProjectDependencyVersions : Task
    {
        [Required]
        public ITaskItem ProjectJson { get; set; }

        public string ValidPrereleaseVersion { get; set; }

        public bool FloatingDependenciesInvalid { get; set; }

        public string OurPackageIdentities { get; set; }

        public override bool Execute()
        {
            Regex ourPackageRegex = null;
            if (!string.IsNullOrEmpty(OurPackageIdentities))
            {
                ourPackageRegex = new Regex(OurPackageIdentities);
            }

            Log.LogMessage(
                "Checking {0}, valid prerelease {1} matching our packages with {2}",
                ProjectJson.ItemSpec,
                ValidPrereleaseVersion,
                OurPackageIdentities);

            using (TextReader projectFileReader = File.OpenText(ProjectJson.ItemSpec))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                JObject projectRoot = serializer.Deserialize<JObject>(projectJsonReader);

                foreach (var package in projectRoot["dependencies"].OfType<JProperty>())
                {
                    var dependencyIdentifier = package.Name;
                    var dependencyVersionRange = VersionRange.Parse(package.Value.ToObject<string>());
                    var dependencyVersion = dependencyVersionRange.MinVersion;

                    string versionMessage = string.Format(
                        "{0} {1} in {2}",
                        dependencyIdentifier,
                        dependencyVersionRange.OriginalString,
                        ProjectJson);

                    if (FloatingDependenciesInvalid && dependencyVersionRange.OriginalString.Contains('*'))
                    {
                        Log.LogError("Floating dependency detected: {0}", versionMessage);
                    }
                    else if (dependencyVersion.IsPrerelease &&
                        ourPackageRegex != null &&
                        ourPackageRegex.IsMatch(dependencyIdentifier) &&
                        !string.IsNullOrEmpty(ValidPrereleaseVersion) &&
                        ValidPrereleaseVersion != dependencyVersion.Release)
                    {
                        Log.LogError(
                            "Expected '{0}' for owned prerelease dependencies, but found '{1}' for {2}",
                            ValidPrereleaseVersion,
                            dependencyVersion.Release,
                            versionMessage);
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
