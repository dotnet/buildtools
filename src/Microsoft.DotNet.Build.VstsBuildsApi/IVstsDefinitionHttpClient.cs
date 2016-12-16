// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.VstsBuildsApi
{
    /// <summary>
    /// A VSTS API client for definitions.
    /// </summary>
    internal interface IVstsDefinitionHttpClient
    {
        /// <summary>
        /// Update VSTS Definition
        /// </summary>
        /// <returns>updated definitions id</returns>
        Task<JObject> VstsUpdateDefinitionAsync(JObject definition);

        /// <summary>
        /// Create VSTS Definition
        /// </summary>
        Task<JObject> VstsCreateDefinitionAsync(JObject definition);

        /// <summary>
        /// Retrieve a VSTS Definition by name and VSTS folder path
        /// </summary>
        Task<JObject> VstsRetrieveDefinitionByIdAsync(JObject definition);

        /// <summary>
        /// Retrieve a VSTS Definition by name and VSTS folder path
        /// </summary>
        Task<IReadOnlyList<JObject>> VstsRetrieveDefinitionsListByNameAndPathAsync(JObject definition);

        /// <summary>
        /// Find all JSON properties in the definition that make it identifiable.
        /// </summary>
        IEnumerable<JProperty> FindIdentifiableDefinitionProperties(JObject definition);
    }
}
