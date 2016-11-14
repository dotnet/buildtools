using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.Build.Tasks
{
    public partial class FilterRuntimesFromSupports
    {
        public static List<CompatibilityProfile> GetAllTfmRidPairs(string supportsFile)
        {
            RuntimeGraph supportsGraph = JsonRuntimeFormat.ReadRuntimeGraph(supportsFile);
            List<CompatibilityProfile> tfmRidPairs = new List<CompatibilityProfile>();
            foreach (var key in supportsGraph.Supports.Keys)
            {
                CompatibilityProfile compatibilityProfile = null;
                supportsGraph.Supports.TryGetValue(key, out compatibilityProfile);
                tfmRidPairs.Add(compatibilityProfile);
            }
            return tfmRidPairs;
        }

        public static List<CompatibilityProfile> FilterForApplicableTFMRIDPairs(List<CompatibilityProfile> tfmRidPairs,
            string framework, string runtimeIdentifier)
        {
            List<CompatibilityProfile> filteredSupports = new List<CompatibilityProfile>();
            List<CompatibilityProfile> filteredRuntimes = new List<CompatibilityProfile>();
            foreach (var compatibilityProfile in tfmRidPairs)
            {
                bool isFrameworkFiltered = false;
                foreach (var tfmRidPair in compatibilityProfile.RestoreContexts)
                {
                    if (NuGetFramework.Parse(framework).Equals(tfmRidPair.Framework))
                    {
                        isFrameworkFiltered = true;
                        filteredSupports.Add(compatibilityProfile);
                        break;
                    }
                    if (isFrameworkFiltered)
                    {
                        break;
                    }
                }
            }

            foreach (var compatibilityProfile in filteredSupports)
            {
                for (int i = compatibilityProfile.RestoreContexts.Count - 1; i >= 0; i--)
                {
                    if (!runtimeIdentifier.Equals(compatibilityProfile.RestoreContexts[i].RuntimeIdentifier))
                    {
                        compatibilityProfile.RestoreContexts.RemoveAt(i);
                    }
                }
                if (compatibilityProfile.RestoreContexts.Count > 0)
                {
                    filteredRuntimes.Add(compatibilityProfile);
                }
            }

            return filteredRuntimes;
        }

        public static JObject GenerateCustomSupportsClause(List<CompatibilityProfile> applicableTfmRidPairs)
        { 
            JObject supportsClause = new JObject();
            foreach (var compatibilityProfile in applicableTfmRidPairs)
            {
                Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();
                foreach (var tfmRidPair in compatibilityProfile.RestoreContexts)
                {
                    if (map.ContainsKey(tfmRidPair.Framework.GetShortFolderName()))
                    {
                        List<string> str = new List<string>();
                        map.TryGetValue(tfmRidPair.Framework.GetShortFolderName(), out str);
                        str.Add(tfmRidPair.RuntimeIdentifier);
                    }
                    else
                    {
                        map.Add(tfmRidPair.Framework.GetShortFolderName(),
                            new List<string>() {tfmRidPair.RuntimeIdentifier});
                    }
                }
                var tfmRidsForSupports = new JObject();
                foreach (var item in map)
                {
                    tfmRidsForSupports[item.Key] = new JArray(item.Value);
                }
                supportsClause[compatibilityProfile.Name] = tfmRidsForSupports;
            }
            return supportsClause;
        }
    }

}