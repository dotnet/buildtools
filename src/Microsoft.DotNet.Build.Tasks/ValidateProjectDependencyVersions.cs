using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ValidateProjectDependencyVersions : VisitProjectDependencies
    {
        private delegate void LogAction(string format, params object[] args);

        private class ValidationPattern
        {
            private Regex _idPattern;
            private string _expectedVersion;
            private string _expectedPrerelease;
            private TaskLoggingHelper _log;

            public ValidationPattern(ITaskItem item, TaskLoggingHelper log)
            {
                string idRegex = item.GetMetadata("IdentityRegex");
                if (string.IsNullOrEmpty(idRegex))
                {
                    // Temporarily support reading the regex from the Include/ItemSpec for backwards compatibility
                    // when the IdentityRegex isn't specified. This can be removed once all consumers are using IdentityRegex.
                    idRegex = item.ItemSpec;
                }

                _idPattern = new Regex(idRegex);
                _expectedVersion = item.GetMetadata("ExpectedVersion");
                _expectedPrerelease = item.GetMetadata("ExpectedPrerelease");
                _log = log;

                if (string.IsNullOrWhiteSpace(_expectedVersion))
                {
                    if (string.IsNullOrWhiteSpace(_expectedPrerelease))
                    {
                        _log.LogError(
                            "Can't find ExpectedVersion or ExpectedPrerelease metadata on item {0}",
                            item.ItemSpec);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(_expectedPrerelease))
                {
                    _log.LogError(
                        "Both ExpectedVersion and ExpectedPrerelease metadata found on item {0}, but only one permitted",
                        item.ItemSpec);
                }
            }

            public bool VisitPackage(
                JProperty package,
                string packageId,
                string version,
                string dependencyMessage,
                bool updateInvalidDependencies)
            {
                bool updatedPackage = false;
                string newVersion = version;

                var dependencyVersionRange = VersionRange.Parse(version);
                NuGetVersion dependencyVersion = dependencyVersionRange.MinVersion;

                LogAction logAction;
                string logPreamble;

                if (updateInvalidDependencies)
                {
                    logAction = _log.LogWarning;
                    logPreamble = "Fixing invalid dependency: ";
                }
                else
                {
                    logAction = _log.LogError;
                    logPreamble = "Dependency validation error: ";
                }
                logPreamble += "for " + dependencyMessage;

                if (_idPattern.IsMatch(packageId))
                {
                    if (!string.IsNullOrWhiteSpace(_expectedVersion) && _expectedVersion != version)
                    {
                        if (updateInvalidDependencies)
                        {
                            newVersion = _expectedVersion;
                            updatedPackage = true;
                        }
                        logAction(
                            "{0} package version is '{1}' but expected '{2}' for packages matching '{3}'",
                            logPreamble,
                            version,
                            _expectedVersion,
                            _idPattern);
                    }
                    if (!string.IsNullOrWhiteSpace(_expectedPrerelease) &&
                        dependencyVersion.IsPrerelease &&
                        _expectedPrerelease != dependencyVersion.Release)
                    {
                        if (updateInvalidDependencies)
                        {
                            newVersion = new NuGetVersion(
                                dependencyVersion.Major,
                                dependencyVersion.Minor,
                                dependencyVersion.Patch,
                                _expectedPrerelease,
                                dependencyVersion.Metadata).ToNormalizedString();
                            updatedPackage = true;
                        }
                        logAction(
                            "{0} package prerelease is '{1}', but expected '{2}' for packages matching '{3}'",
                            logPreamble,
                            dependencyVersion.Release,
                            _expectedPrerelease,
                            _idPattern);
                    }
                }
                if (updatedPackage)
                {
                    if (package.Value is JObject)
                    {
                        package.Value["version"] = newVersion;
                    }
                    else
                    {
                        package.Value = newVersion;
                    }
                }
                return updatedPackage;
            }
        }

        /// <summary>
        /// Prohibits floating dependencies, aka "*" dependencies. Defaults to false, allowing them.
        /// </summary>
        public bool ProhibitFloatingDependencies { get; set; }

        /// <summary>
        /// A set of patterns to enforce for package dependencies. If not specified, all
        /// versions are permitted for any package.
        /// </summary>
        public ITaskItem[] ValidationPatterns { get; set; }

        /// <summary>
        /// If true, when an invalid dependency is encountered it is changed to the valid version.
        /// </summary>
        public bool UpdateInvalidDependencies { get; set; }

        public override bool VisitPackage(JProperty package, string projectJsonPath)
        {
            var patterns = Enumerable.Empty<ValidationPattern>();

            if (ValidationPatterns != null)
            {
                patterns = ValidationPatterns
                    .Select(item => new ValidationPattern(item, Log))
                    .ToArray();
            }

            string id = package.Name;
            string version;
            if (package.Value is JObject)
            {
                JToken target = package.Value["target"];
                if (target != null && target.Value<string>() == "project")
                {
                    return false;
                }

                version = package.Value["version"].Value<string>();
            }
            else if (package.Value is JValue)
            {
                version = package.Value.ToObject<string>();
            }
            else
            {
                throw new ArgumentException(string.Format(
                    "Unrecognized dependency element for {0} in {1}",
                    package.Name,
                    projectJsonPath));
            }

            string dependencyMessage = string.Format(
                "{0} {1} in {2}",
                id,
                version,
                projectJsonPath);

            bool packageUpdated = false;

            foreach (var pattern in patterns)
            {
                packageUpdated |= pattern.VisitPackage(
                    package,
                    id,
                    version,
                    dependencyMessage,
                    UpdateInvalidDependencies);
            }

            if (!packageUpdated && ProhibitFloatingDependencies && version.Contains('*'))
            {
                // A * dependency was found but it hasn't been fixed. It might not have been fixed
                // because UpdateInvalidDependencies = false or because a pattern didn't match it:
                // either way this is an error.
                Log.LogError("Floating dependency detected: {0}", dependencyMessage);
            }

            return packageUpdated;
        }
    }
}
