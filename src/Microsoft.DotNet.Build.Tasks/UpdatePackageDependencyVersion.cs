using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
