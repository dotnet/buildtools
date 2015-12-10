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
    public abstract class VisitProjectDependencies : Task
    {
        [Required]
        public ITaskItem[] ProjectJsons { get; set; }

        public static JObject ReadJsonFile(string jsonPath)
        {
            using (TextReader projectFileReader = File.OpenText(jsonPath))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }

        public static void WriteJsonFile(JObject projectRoot, string jsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);

            File.WriteAllText(jsonPath, projectJson + Environment.NewLine);
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
                    WriteJsonFile(projectRoot, projectJsonPath);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
