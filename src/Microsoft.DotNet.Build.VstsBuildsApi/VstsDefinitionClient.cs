// Licensed to the .NET Foundation under one or more agreements. 
// The .NET Foundation licenses this file to you under the MIT license. 
// See the LICENSE file in the project root for more information. 

using Microsoft.DotNet.Build.VstsBuildsApi.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.VstsBuildsApi
{
    public class VstsDefinitionClient
    {
        private string _collectionIdentifier;
        private VstsDefinitionClientConfig _config;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="collectionIdentifier">collection identifer for build and release definitions</param>
        /// <param name="config">Configuration for VSTS definition API endpoints.</param>
        public VstsDefinitionClient(string collectionIdentifier, VstsDefinitionClientConfig config)
        {
            if (string.IsNullOrWhiteSpace(collectionIdentifier))
            {
                throw new ArgumentException("Required parameter (collectionIdentifier) cannot be null or white space.");
            }
            if (!Uri.IsWellFormedUriString(collectionIdentifier, UriKind.Relative))
            {
                throw new ArgumentException($"collectionIdentifier '{collectionIdentifier}' contains invalid characters.  collectionIdentifier must contain only valid uri characters.");
            }

            _collectionIdentifier = collectionIdentifier;
            _config = config;
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
            var client = new VstsBuildHttpClient(definition, _config.BuildDefinitionEndpointConfig);
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
            var client = new VstsReleaseHttpClient(definition, _config.ReleaseDefinitionEndpointConfig);
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
            VstsDefinitionHttpClient client,
            JObject definition)
        {
            var key = _collectionIdentifier + "_" + definition["name"];
            definition["name"] = key;
            IReadOnlyList<JObject> vstsDefinitions = await client.RetrieveDefinitionsListByNameAndPathAsync(definition).ConfigureAwait(false);

            if (vstsDefinitions.Count == 0)
            {
                /* Create */

                /* Remove definition instance identifiable information */
                RemoveIdentifiableInformation(client, definition);

                JObject vstsDefinition = await client.CreateDefinitionAsync(definition).ConfigureAwait(false);
                return vstsDefinition["id"].ToString();
            }
            if (vstsDefinitions.Count == 1)
            {
                JObject vstsDefinition = await client.RetrieveDefinitionByIdAsync(vstsDefinitions[0]).ConfigureAwait(false);

                /* Update */
                if (!IsDefinitionContentSubsetEquivalent(client, definition, vstsDefinition))
                {
                    CopyIdentifiableInformation(client, vstsDefinition, definition);
                    vstsDefinition = await client.UpdateDefinitionAsync(definition).ConfigureAwait(false);
                }
                return vstsDefinition["id"].ToString();
            }
            throw new InvalidOperationException(
                $"Obtained multiple {vstsDefinitions.Count} definitions with the same " +
                $"'name' ({vstsDefinitions[0]["name"]}) and " +
                $"'path' ({vstsDefinitions[0]["path"]}) properties.  " +
                "This should not be possible.");
        }

        /// <summary>
        /// Create a default endpoint configuration.
        /// </summary>
        /// <param name="credentials">Credentials to use to access all endpoints.</param>
        /// <returns>Default authenticated endpoint configuration.</returns>
        public static VstsDefinitionClientConfig CreateDefaultConfig(string credentials)
        {
            return new VstsDefinitionClientConfig
            {
                BuildDefinitionEndpointConfig = new VstsApiEndpointConfig
                {
                    Credentials = credentials,
                    ApiVersion = "2.0",
                    InstanceIdentifiableFields = new[]
                    {
                        "_links",
                        "authoredBy",
                        "comment",
                        "createdDate",
                        "id",
                        "path",
                        "revision",
                        "uri",
                        "url",
                    }
                },
                ReleaseDefinitionEndpointConfig = new VstsApiEndpointConfig
                {
                    Credentials = credentials,
                    ApiVersion = "3.1-preview.3",
                    InstanceIdentifiableFields = new[]
                    {
                        "_links",
                        "createdBy",
                        "createdOn",
                        "deployStep",
                        "modifiedBy",
                        "modifiedOn",
                        "owner",
                        "id",
                        "lastRelease",
                        "revision",
                    }
                }
            };
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
            VstsDefinitionHttpClient client,
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

        private static void RemoveIdentifiableInformation(VstsDefinitionHttpClient client, params JObject[] definitions)
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
            VstsDefinitionHttpClient client,
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
