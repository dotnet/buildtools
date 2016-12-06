// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        //This should be the long form of the Nuget Framework moniker.
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
                    tfmRidPairs.Add(new TfmRidPair(tfmRidPair.Framework.ToString(), tfmRidPair.RuntimeIdentifier));
                }
            }
            return tfmRidPairs;
        }

        public static List<TfmRidPair> FilterForApplicableTFMRIDPairs(List<TfmRidPair> tfmRidPairs,
            string framework, string runtimeIdentifier)
        {
            if (framework != null)
            {
                string longFramework = NuGetFramework.Parse(framework).ToString();
                tfmRidPairs.RemoveAll(x => !longFramework.Equals(x.framework));
            }
            if (runtimeIdentifier != null)
            {
                tfmRidPairs.RemoveAll(x => !runtimeIdentifier.Equals(x.runtimeIdentifier));
            }
            
            return tfmRidPairs;
        }

        public static JObject GenerateCustomSupportsClause(List<TfmRidPair> applicableTfmRidPairs)
        { 
            JObject supportsClause = new JObject();
            var tfmRidsForSupports = new JObject();
            foreach (var tfmRidPair in applicableTfmRidPairs)
            {
                tfmRidsForSupports[NuGetFramework.Parse(tfmRidPair.framework).GetShortFolderName()] = new JArray(tfmRidPair.runtimeIdentifier);
            }
            supportsClause["corefx.Test"] = tfmRidsForSupports;
            return supportsClause;
        }
    }

}