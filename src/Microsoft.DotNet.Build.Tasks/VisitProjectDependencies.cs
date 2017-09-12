using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public abstract class VisitProjectDependencies : BuildTask
    {
        [Required]
        public ITaskItem[] ProjectJsons { get; set; }

        public static JObject ReadJsonFile(string projectJsonPath)
        {
            using (TextReader reader = File.OpenText(projectJsonPath))
            {
                var projectJsonReader = new JsonTextReader(reader);

                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }

        private static void WriteProject(JObject projectRoot, string projectJsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);

            File.WriteAllText(projectJsonPath, projectJson + Environment.NewLine);
        }

        private static IEnumerable<JProperty> FindAllDependencyProperties(JObject projectJsonRoot)
        {
            return projectJsonRoot
                .Descendants()
                .OfType<JProperty>()
                .Where(property => property.Name == "dependencies")
                .Select(property => property.Value)
                .SelectMany(o => o.Children<JProperty>());
        }

        /// <summary>
        /// Visit a package.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="projectJsonPath"></param>
        /// <returns>True if a change was made that needs to be saved back to the project.json.</returns>
        public abstract bool VisitPackage(JProperty package, string projectJsonPath);

        public override bool Execute()
        {
            foreach (var projectJsonPath in ProjectJsons.Select(item => item.ItemSpec))
            {
                JObject projectRoot = ReadJsonFile(projectJsonPath);

                bool changedAnyPackage = FindAllDependencyProperties(projectRoot)
                    .Select(package => VisitPackage(package, projectJsonPath))
                    .ToArray()
                    .Any(shouldWrite => shouldWrite);

                if (changedAnyPackage)
                {
                    Log.LogMessage("Writing changes to {0}", projectJsonPath);
                    WriteProject(projectRoot, projectJsonPath);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
