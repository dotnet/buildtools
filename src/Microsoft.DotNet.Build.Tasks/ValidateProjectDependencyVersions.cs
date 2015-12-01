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
        private class ValidationPattern
        {
            private Regex _idPattern;
            private string _expectedVersion;
            private string _expectedPrerelease;

            public ValidationPattern(ITaskItem item, TaskLoggingHelper log)
            {
                _idPattern = new Regex(item.ItemSpec);
                _expectedVersion = item.GetMetadata("ExpectedVersion");
                _expectedPrerelease = item.GetMetadata("ExpectedPrerelease");

                if (string.IsNullOrWhiteSpace(_expectedVersion))
                {
                    if (string.IsNullOrWhiteSpace(_expectedPrerelease))
                    {
                        log.LogError(
                            "Can't find ExpectedVersion or ExpectedPrerelease metadata on item {0}",
                            item.ItemSpec);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(_expectedPrerelease))
                {
                    log.LogError(
                        "Both ExpectedVersion and ExpectedPrerelease metadata found on item {0}, but only one permitted",
                        item.ItemSpec);
                }
            }

            public void Validate(string packageId, string version, TaskLoggingHelper log, string dependencyMessage)
            {
                var dependencyVersionRange = VersionRange.Parse(version);
                NuGetVersion dependencyVersion = dependencyVersionRange.MinVersion;

                if (_idPattern.IsMatch(packageId))
                {
                    if (!string.IsNullOrWhiteSpace(_expectedVersion) && _expectedVersion != version)
                    {
                        log.LogError(
                            "Dependency validation error: package version '{0}', but expected '{1}' for packages matching '{2}' for {3}",
                            version,
                            _expectedVersion,
                            _idPattern,
                            dependencyMessage);
                    }
                    if (!string.IsNullOrWhiteSpace(_expectedPrerelease) &&
                        dependencyVersion.IsPrerelease &&
                        _expectedPrerelease != dependencyVersion.Release)
                    {
                        log.LogError(
                            "Dependency validation error: package prerelease '{0}', but expected '{1}' for packages matching '{2}' for {3}",
                            dependencyVersion.Release,
                            _expectedPrerelease,
                            _idPattern,
                            dependencyMessage);
                    }
                }
            }
        }

        [Required]
        public ITaskItem ProjectJson { get; set; }

        public bool ProhibitFloatingDependencies { get; set; }

        public ITaskItem[] ValidationPatterns { get; set; }

        public override bool Execute()
        {
            var patterns = Enumerable.Empty<ValidationPattern>();

            if (ValidationPatterns != null)
            {
                patterns = ValidationPatterns
                    .Select(item => new ValidationPattern(item, Log))
                    .ToArray();
            }

            using (TextReader projectFileReader = File.OpenText(ProjectJson.ItemSpec))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                JObject projectRoot = serializer.Deserialize<JObject>(projectJsonReader);

                var dependencyObjects = projectRoot
                    .Descendants()
                    .OfType<JProperty>()
                    .Where(property => property.Name == "dependencies")
                    .Select(property => property.Value);

                foreach (var package in dependencyObjects.SelectMany(o => o.Children<JProperty>()))
                {
                    string id = package.Name;
                    string version = package.Value.ToObject<string>();

                    string dependencyMessage = string.Format(
                        "{0} {1} in {2}",
                        id,
                        version,
                        ProjectJson);

                    if (ProhibitFloatingDependencies && version.Contains('*'))
                    {
                        Log.LogError("Floating dependency detected: {0}", dependencyMessage);
                    }
                    else
                    {
                        foreach (var pattern in patterns)
                        {
                            pattern.Validate(id, version, Log, dependencyMessage);
                        }
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
