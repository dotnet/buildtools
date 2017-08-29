using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ValidateExactRestore : BuildTask
    {
        [Required]
        public ITaskItem[] ProjectLockJsons { get; set; }

        private HashSet<string> _errors = new HashSet<string>();

        public override bool Execute()
        {
            foreach (var projectLockItem in ProjectLockJsons)
            {
                ValidateLockFile(projectLockItem.ItemSpec);
            }
            return !Log.HasLoggedErrors;
        }

        private void ValidateLockFile(string lockFilePath)
        {
            var lockfile = VisitProjectDependencies.ReadJsonFile(lockFilePath);

            var lockedDependencyVersions = lockfile["libraries"]
                .Children<JProperty>()
                .Select(p => p.Name.Split('/'))
                .ToLookup(libParts => libParts[0], libParts => NuGetVersion.Parse(libParts[1]));

            // Look at dependencies for all frameworks.
            var requestedDependencies = lockfile["projectFileDependencyGroups"]
                .Children<JProperty>()
                .SelectMany(group => group.Value.Value<JArray>().Values<string>());

            foreach (var dependency in requestedDependencies)
            {
                string[] dependencyParts = dependency.Split(' ');
                string requestedId = dependencyParts[0];
                if (dependencyParts.Length < 3)
                {
                    // Some dependencies have no versions, e.g. 'test-runtime' and 'net46-test-runtime'.
                    continue;
                }

                string requestedVersion = dependencyParts[2];
                NuGetVersion requestedNuGetVersion = NuGetVersion.Parse(requestedVersion);

                IEnumerable<NuGetVersion> restoredVersions = lockedDependencyVersions[requestedId];

                // Check if the requested package is exactly included in the "libraries" section.
                if (!restoredVersions.Contains(requestedNuGetVersion))
                {
                    HandleNonExistentDependency(requestedId, requestedVersion, restoredVersions, lockFilePath);
                }
            }
        }

        protected virtual void HandleNonExistentDependency(
            string name,
            string version,
            IEnumerable<NuGetVersion> libraryVersionsRestored,
            string lockFilePath)
        {
            string errorMessage = string.Format(
                "Exact package '{0} {1}' was not restored: found '{2}'",
                name,
                version,
                string.Join(", ", libraryVersionsRestored));

            // Only output message once per combination.
            if (_errors.Add(errorMessage))
            {
                Log.LogError(errorMessage);
            }

            Log.LogMessage(MessageImportance.Low, $"{errorMessage} in '{lockFilePath}'");
        }
    }
}
