using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.Build.Tasks
{
    public class TfmRidPair
    {
        public TfmRidPair(string tfm, string rid)
        {
            framework = tfm;
            runtimeIdentifier = rid;
        }
        public string framework { get; set; }
        public string runtimeIdentifier { get; set; }
    }
    public partial class FilterRuntimesFromSupports
    {
        public static List<TfmRidPair> GetAllTfmRidPairs(string supportsFile)
        {
            RuntimeGraph supportsGraph = JsonRuntimeFormat.ReadRuntimeGraph(supportsFile);
            List<TfmRidPair> tfmRidPairs = new List<TfmRidPair>();
            foreach (var key in supportsGraph.Supports.Keys)
            {
                CompatibilityProfile compatibilityProfile = null;
                supportsGraph.Supports.TryGetValue(key, out compatibilityProfile);
                foreach (var tfmRidPair in compatibilityProfile.RestoreContexts)
                {
                    tfmRidPairs.Add(new TfmRidPair(tfmRidPair.Framework.GetShortFolderName(), tfmRidPair.RuntimeIdentifier));
                }
            }
            return tfmRidPairs;
        }

        public static List<TfmRidPair> FilterForApplicableTFMRIDPairs(List<TfmRidPair> tfmRidPairs,
            string framework, string runtimeIdentifier)
        {
            tfmRidPairs.RemoveAll(x => !framework.Equals(x.framework));
            tfmRidPairs.RemoveAll(x => !runtimeIdentifier.Equals(x.runtimeIdentifier));
            
            return tfmRidPairs;
        }

        public static JObject GenerateCustomSupportsClause(List<TfmRidPair> applicableTfmRidPairs)
        { 
            JObject supportsClause = new JObject();
            var tfmRidsForSupports = new JObject();
            foreach (var tfmRidPair in applicableTfmRidPairs)
            {
                tfmRidsForSupports[tfmRidPair.framework] = new JArray(tfmRidPair.runtimeIdentifier);
            }
            supportsClause["corefx.Test"] = tfmRidsForSupports;
            return supportsClause;
        }
    }

}