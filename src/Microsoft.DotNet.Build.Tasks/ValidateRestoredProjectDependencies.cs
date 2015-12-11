using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ValidateRestoredProjectDependencies : Task
    {
        [Required]
        public ITaskItem[] ProjectLockJsons { get; set; }

        public override bool Execute()
        {
            foreach (var projectLockItem in ProjectLockJsons)
            {
                var lockfile = VisitProjectDependencies.ReadJsonFile(projectLockItem.ItemSpec);
                ValidateLockFile(lockfile, projectLockItem.ItemSpec);
            }

            return !Log.HasLoggedErrors;
        }

        private void ValidateLockFile(JObject lockfile, string lockFilePath)
        {
            var lockedDependencyVersions = lockfile["libraries"]
                .Children<JProperty>()
                .Select(p => p.Name.Split('/'))
                .ToLookup(libParts => libParts[0], libParts => libParts[1]);

            var requestedDependencies = lockfile["projectFileDependencyGroups"]
                .Children<JProperty>()
                .Where(group => group.Name == "")
                .SelectMany(group => group.Value.Value<JArray>().Values<string>());

            foreach (string dependency in requestedDependencies)
            {
                string[] dependencyParts = dependency.Split(' ');
                string requestedId = dependencyParts[0];
                string requestedVersion = dependencyParts[2];

                IEnumerable<string> versionsRestored = lockedDependencyVersions[requestedId];

                if (!versionsRestored.Contains(requestedVersion))
                {
                    HandleNonExistentDependency(requestedId, requestedVersion, versionsRestored, lockFilePath);
                }
            }
        }

        protected virtual void HandleNonExistentDependency(
            string name,
            string version,
            IEnumerable<string> libraryVersionsRestored,
            string lockFilePath)
        {
            Log.LogError(
                "Desired version {0} {1} not restored, found '{2}' for {3}",
                name,
                version,
                string.Join(", ", libraryVersionsRestored),
                lockFilePath);
        }
    }
}
