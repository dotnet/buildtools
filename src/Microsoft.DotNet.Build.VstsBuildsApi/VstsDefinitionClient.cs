// Licensed to the .NET Foundation under one or more agreements. 
// The .NET Foundation licenses this file to you under the MIT license. 
// See the LICENSE file in the project root for more information. 

using Microsoft.DotNet.Build.VstsBuildsApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace VstsBuildsApi
{
    public class VstsDefinitionClient
    {
        private string _collectionIdentifier;
        private string _credentials;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="collectionIdentifier">collection identifer for build and release definitions</param>
        /// <param name="credentials">credentials used for basic rest api authentication</param>
        public VstsDefinitionClient(string collectionIdentifier, string credentials)
        {
            if (string.IsNullOrWhiteSpace(collectionIdentifier))
            {
                throw new Exception("Required parameter (collectionIdentifier) cannot be null or white space.");
            }
            if (!Uri.IsWellFormedUriString(collectionIdentifier, UriKind.Relative))
            {
                throw new Exception(string.Format("collectionIdentifier '{0}' contains invalid characters.  collectionIdentifier must contain only valid uri characters.", collectionIdentifier));
            }

            _collectionIdentifier = collectionIdentifier;
            _credentials = credentials;
        }

        /// <summary>
        /// Load a local build definition, combine the uniqueIdentifier with the definition name, 
        /// check VSTS for a matching definition name, if present, update that definition with the
        /// local build definition, otherwise create a new definition.
        /// </summary>
        /// <param name="stream">Stream to a VSTS build definition</param>
        /// <returns>Created or updated build definition id</returns>
        public async Task<string> CreateOrUpdateBuildDefinitionAsync(Stream stream)
        {
            JObject definition = GetParsedJson(stream);
            var client = new VstsBuildHttpClient(definition, _credentials);
            return await CreateOrUpdateDefinitionAsync(client, definition).ConfigureAwait(false);
        }

        /// <summary>
        /// Load a local release definition, combine the uniqueIdentifier with the definition name,
        /// check VSTS for a matching definition name, if present, update that definition with the
        /// local release definition, otherwise create a new definition.
        /// </summary>
        /// <param name="stream">Stream to a VSTS release definition</param>
        /// <returns>Created or updated release definition id</returns>
        public async Task<string> CreateOrUpdateReleaseDefinitionAsync(Stream stream)
        {
            JObject definition = GetParsedJson(stream);
            var client = new VstsReleaseHttpClient(definition, _credentials);
            return await CreateOrUpdateDefinitionAsync(client, definition).ConfigureAwait(false);
        }

        /// <summary>
        /// Load a local definition, combine the uniqueIdentifier with the definition name, 
        /// check VSTS for a matching definition name, if present, update that definition with the
        /// local definition, otherwise create a new definition.
        /// </summary>
        /// <param name="client">Definition client that interacts with VSTS API and understands
        /// definition JSON content.</param>
        /// <param name="definition">VSTS definition JSON object</param>
        /// <returns>Created or updated definition id</returns>
        private async Task<string> CreateOrUpdateDefinitionAsync(
            IVstsDefinitionHttpClient client,
            JObject definition)
        {
            var key = _collectionIdentifier + "_" + definition["name"];
            definition["name"] = key;
            IReadOnlyList<JObject> vstsDefinitions = await client.VstsRetrieveDefinitionsListByNameAndPathAsync(definition).ConfigureAwait(false);

            if (vstsDefinitions.Count == 0)
            {
                /* Create */

                /* Remove definition instance identifiable information */
                RemoveIdentifiableInformation(client, definition);

                JObject vstsDefinition = await client.VstsCreateDefinitionAsync(definition).ConfigureAwait(false);
                return vstsDefinition["id"].ToString();
            }
            else if (vstsDefinitions.Count == 1)
            {
                JObject vstsDefinition = await client.VstsRetrieveDefinitionByIdAsync(vstsDefinitions[0]).ConfigureAwait(false);

                /* Update */
                if (!IsDefinitionContentSubsetEquivalent(client, definition, vstsDefinition))
                {
                    CopyIdentifiableInformation(client, vstsDefinition, definition);
                    vstsDefinition = await client.VstsUpdateDefinitionAsync(definition).ConfigureAwait(false);
                }
                return vstsDefinition["id"].ToString();
            }
            else
            {
                throw new Exception(string.Format(
                    "Obtained multiple {0} definitions with the same 'name' ({1}) and 'path' ({2}) properties.  This should not be possible.",
                    vstsDefinitions.Count,
                    vstsDefinitions[0]["name"].ToString(),
                    vstsDefinitions[0]["path"].ToString()));
            }
        }

        private static JObject GetParsedJson(Stream stream)
        {
            using (StreamReader streamReader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
            {
                return (JObject)JToken.ReadFrom(jsonReader);
            }
        }

        /// <summary>
        /// Validates that definition1 is a content equivalent subset of definition2
        /// </summary>
        private static bool IsDefinitionContentSubsetEquivalent(
            IVstsDefinitionHttpClient client,
            JObject definition1,
            JObject definition2)
        {
            JObject clonedDefinition1 = (JObject)definition1.DeepClone();
            JObject clonedDefinition2 = (JObject)definition2.DeepClone();

            RemoveIdentifiableInformation(client, clonedDefinition1, clonedDefinition2);

            // Compare only the child tokens present in the first definition to the corresponding contents of the second definition
            // The second definition may contain additional tokens which we don't care about.
            foreach (var childToken in clonedDefinition1.Children())
            {
                if (!JToken.DeepEquals(childToken.First, clonedDefinition2[childToken.Path]))
                {
                    return false;
                }
            }
            return true;
        }

        private static void RemoveIdentifiableInformation(IVstsDefinitionHttpClient client, params JObject[] definitions)
        {
            foreach (var definition in definitions)
            {
                foreach (JProperty property in client.FindIdentifiableDefinitionProperties(definition))
                {
                    property.Remove();
                }
            }
        }

        /// <summary>
        /// Copy all needed information according to <paramref name="client"/> from
        /// <paramref name="source"/> to <paramref name="destination"/>.
        /// </summary>
        private static void CopyIdentifiableInformation(
            IVstsDefinitionHttpClient client,
            JObject source,
            JObject destination)
        {
            foreach (JProperty property in client.FindIdentifiableDefinitionProperties(source))
            {
                // Find the parent object of the property in destination and assign value by name.
                destination.SelectToken(property.Parent.Path)[property.Name] = property.Value;
            }
        }
    }
}
