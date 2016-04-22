using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class UpdatePackageDependencyVersion : VisitProjectDependencies
    {
        [Required]
        public string PackageId { get; set; }

        [Required]
        public string OldVersion { get; set; }

        [Required]
        public string NewVersion { get; set; }

        public override bool VisitPackage(JProperty package, string projectJsonPath)
        {
            var dependencyIdentifier = package.Name;
            string dependencyVersion;
            if (package.Value is JObject)
            {
                dependencyVersion = package.Value["version"]?.Value<string>();
            }
            else if (package.Value is JValue)
            {
                dependencyVersion = package.Value.ToObject<string>();
            }
            else
            {
                throw new ArgumentException(string.Format(
                    "Unrecognized dependency element for {0} in {1}",
                    package.Name,
                    projectJsonPath));
            }

            if (dependencyVersion == null)
            {
                return false;
            }

            if (dependencyIdentifier == PackageId && dependencyVersion == OldVersion)
            {
                Log.LogMessage(
                    "Changing {0} {1} to {2} in {3}",
                    dependencyIdentifier,
                    dependencyVersion,
                    NewVersion,
                    projectJsonPath);

                if (package.Value is JObject)
                {
                    package.Value["version"] = NewVersion;
                }
                else
                {
                    package.Value = NewVersion;
                }
                return true;
            }
            return false;
        }
    }
}
