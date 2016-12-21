// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Build.VstsBuildsApi.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.VstsBuildsApi
{
    /// <summary>
    /// A VSTS API client for definitions.
    /// </summary>
    internal abstract class VstsDefinitionHttpClient
    {
        private VstsApiEndpointConfig _config;

        protected JsonHttpClient JsonClient { get; }

        protected VstsDefinitionHttpClient(Uri baseAddress, VstsApiEndpointConfig config)
        {
            _config = config;
            JsonClient = new JsonHttpClient(
                new Uri($"{baseAddress.Scheme}://{baseAddress.Authority}/"),
                config.Credentials);
        }

        /// <summary>
        /// Update VSTS Definition
        /// </summary>
        /// <returns>updated definitions id</returns>
        public virtual async Task<JObject> UpdateDefinitionAsync(JObject definition) =>
            await JsonClient.PutAsync(
                GetRequestUri(definition, $"definitions/{definition["id"]}"),
                definition);

        /// <summary>
        /// Create VSTS Definition
        /// </summary>
        public async Task<JObject> CreateDefinitionAsync(JObject definition) =>
            await JsonClient.PostAsync(
                GetRequestUri(definition, "definitions"),
                definition);

        /// <summary>
        /// Retrieve a VSTS Definition by name and VSTS folder path
        /// </summary>
        public async Task<JObject> RetrieveDefinitionByIdAsync(JObject definition) =>
            await JsonClient.GetAsync(
                GetRequestUri(definition, $"definitions/{definition["id"]}"));

        /// <summary>
        /// Retrieve a VSTS Definition by name and VSTS folder path
        /// </summary>
        public async Task<IReadOnlyList<JObject>> RetrieveDefinitionsListByNameAndPathAsync(JObject definition)
        {
            string requestUrl = GetRequestUri(
                definition,
                "definitions",
                new Dictionary<string, string>
                {
                    ["name"] = definition["name"].Value<string>()
                });

            JObject definitionsJObject = await JsonClient.GetAsync(requestUrl);

            if (int.Parse(definitionsJObject["count"].Value<string>()) > 0)
            {
                return definitionsJObject["value"]
                    .Children()
                    .Cast<JObject>()
                    .Where(found => IsMatching(definition, found))
                    .ToArray();
            }
            return new JObject[0];
        }

        /// <summary>
        /// Find all JSON properties in the definition that make it identifiable.
        /// </summary>
        public IEnumerable<JProperty> FindIdentifiableDefinitionProperties(JObject definition)
        {
            return FindObjectsWithIdentifiableProperties(definition)
                .SelectMany(FindIdentifiableProperties);
        }

        /// <summary>
        /// Get the uri needed to make a request.
        /// </summary>
        /// <param name="definition">A definition containing the project name.</param>
        /// <param name="command">The API command to run. May include forward slashes to add Uri args.</param>
        /// <param name="args">A dictionary of query arguments. Api version is automatically added.</param>
        /// <returns>The request uri needed to make the request, as a string.</returns>
        protected string GetRequestUri(JObject definition, string command, Dictionary<string, string> args = null)
        {
            if (args == null)
            {
                args = new Dictionary<string, string>();
            }
            args["api-version"] = _config.ApiVersion;
            string argsQuery = string.Join("&", args.Select(arg => $"{arg.Key}={Uri.EscapeDataString(arg.Value)}"));

            return $"DefaultCollection/{GetDefinitionProject(definition)}/_apis/{ApiType}/{command}?{argsQuery}";
        }

        /// <summary>
        /// Finds all objects in the definition that may have identifiable properties.
        /// </summary>
        protected virtual IEnumerable<JObject> FindObjectsWithIdentifiableProperties(JObject definition)
        {
            yield return definition;
        }

        /// <summary>
        /// From a definition object, get the project it belongs to.
        /// </summary>
        protected abstract string GetDefinitionProject(JObject definition);

        /// <summary>
        /// The type of API this is: the path segment preceding the "command" in GetRequestUri.
        /// </summary>
        protected abstract string ApiType { get; }

        /// <summary>
        /// Determine if the definitions match well enough that replacing the retrieved definition
        /// with the local definition is correct.
        /// </summary>
        protected abstract bool IsMatching(JObject localDefinition, JObject retrievedDefinition);

        private IEnumerable<JProperty> FindIdentifiableProperties(JObject obj)
        {
            return _config.InstanceIdentifiableFields
                .Select(obj.Property)
                .Where(property => property != null);
        }
    }
}
