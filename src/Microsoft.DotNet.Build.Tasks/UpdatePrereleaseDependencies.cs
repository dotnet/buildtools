using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks
{
    public class UpdatePrereleaseDependencies : VisitProjectDependencies
    {
        [Required]
        public string OldPrerelease { get; set; }

        [Required]
        public string NewPrerelease { get; set; }

        public override bool VisitPackage(JProperty package, string projectJsonPath)
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
}
