using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
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

            public ValidationPattern(ITaskItem item)
            {
                _idPattern = new Regex(item.ItemSpec);
                _expectedVersion = item.GetMetadata("ExpectedVersion");
                _expectedPrerelease = item.GetMetadata("ExpectedPrerelease");

                if (string.IsNullOrWhiteSpace(_expectedVersion))
                {
                    if (string.IsNullOrWhiteSpace(_expectedPrerelease))
                    {
                        throw new ArgumentException(string.Format(
                            "Can't find ExpectedVersion or ExpectedPrerelease metadata on item {0}",
                            item.ItemSpec));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(_expectedPrerelease))
                {
                    throw new ArgumentException(string.Format(
                        "Both ExpectedVersion and ExpectedPrerelease metadata found on item {0}, but only one permitted",
                        item.ItemSpec));
                }
            }

            public string FindValidationError(string packageId, string version)
            {
                var dependencyVersionRange = VersionRange.Parse(version);
                NuGetVersion dependencyVersion = dependencyVersionRange.MinVersion;

                if (_idPattern.IsMatch(packageId))
                {
                    if (!string.IsNullOrWhiteSpace(_expectedVersion) && _expectedVersion != version)
                    {
                        return string.Format(
                            "package version '{0}', but expected '{1}' for packages matching '{2}'",
                            version,
                            _expectedVersion,
                            _idPattern);
                    }
                    if (!string.IsNullOrWhiteSpace(_expectedPrerelease) &&
                        dependencyVersion.IsPrerelease &&
                        _expectedPrerelease != dependencyVersion.Release)
                    {
                        return string.Format(
                            "package prerelease '{0}', but expected '{1}' for packages matching '{2}'",
                            dependencyVersion.Release,
                            _expectedPrerelease,
                            _idPattern);
                    }
                }
                return null;
            }
        }

        [Required]
        public ITaskItem ProjectJson { get; set; }

        public bool ProhibitFloatingDependencies { get; set; }

        public ITaskItem[] ValidationPatterns { get; set; }

        public override bool Execute()
        {
            var patterns = ValidationPatterns.Select(item => new ValidationPattern(item));

            using (TextReader projectFileReader = File.OpenText(ProjectJson.ItemSpec))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                JObject projectRoot = serializer.Deserialize<JObject>(projectJsonReader);

                foreach (var package in projectRoot["dependencies"].OfType<JProperty>())
                {
                    string id = package.Name;
                    string version = package.Value.ToObject<string>();

                    string versionMessage = string.Format(
                        "{0} {1} in {2}",
                        id,
                        version,
                        ProjectJson);

                    if (ProhibitFloatingDependencies && version.Contains('*'))
                    {
                        Log.LogError("Floating dependency detected: {0}", versionMessage);
                    }
                    else
                    {
                        string validationError = patterns
                            .Select(pattern => pattern.FindValidationError(id, version))
                            .FirstOrDefault(error => error != null);

                        if (validationError != null)
                        {
                            Log.LogError(
                                "Dependency validation error: {0} for {1}",
                                validationError,
                                versionMessage);
                        }
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
