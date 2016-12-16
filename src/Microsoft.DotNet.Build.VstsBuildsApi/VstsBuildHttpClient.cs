// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.VstsBuildsApi
{
    internal class VstsBuildHttpClient : IVstsDefinitionHttpClient
    {
        private static readonly string[] s_InstanceIdentifiableField = {
            "_links",
            "authoredBy",
            "comment",
            "createdDate",
            "id",
            "path",
            "revision",
            "uri",
            "url",
        };

        private JsonHttpClient _jsonClient;

        public VstsBuildHttpClient(JObject definition, string credentials)
        {
            Uri uri = new Uri(GetVstsBuildsApiUrl(definition));

            _jsonClient = new JsonHttpClient(
                new Uri(string.Format("{0}://{1}/", uri.Scheme, uri.Authority)),
                credentials);
        }

        public async Task<JObject> VstsUpdateDefinitionAsync(JObject definition)
        {
            string requestUri = $"DefaultCollection/{VstsBuildsApiBase(definition)}/_apis/build/definitions/{definition["id"].ToString()}?api-version=2.0";
            return await _jsonClient.PutAsync(requestUri, definition);
        }

        public async Task<JObject> VstsCreateDefinitionAsync(JObject definition)
        {
            string requestUri = $"DefaultCollection/{VstsBuildsApiBase(definition)}/_apis/build/definitions?api-version=2.0";
            return await _jsonClient.PostAsync(requestUri, definition);
        }

        public async Task<JObject> VstsRetrieveDefinitionByIdAsync(JObject definition)
        {
            string requestUri = $"DefaultCollection/{VstsBuildsApiBase(definition)}/_apis/build/definitions/{definition["id"].ToString()}?api-version=2.0";
            return await _jsonClient.GetAsync(requestUri);
        }

        public async Task<IReadOnlyList<JObject>> VstsRetrieveDefinitionsListByNameAndPathAsync(JObject definition)
        {
            string requestUri = $"DefaultCollection/{VstsBuildsApiBase(definition)}/_apis/build/definitions?api-version=2.0&name={definition["name"].ToString()}";

            JObject definitionsJObject = await _jsonClient.GetAsync(requestUri);
            List<JObject> definitions = new List<JObject>();
            if (int.Parse(definitionsJObject["count"].Value<string>()) > 0)
            {
                var children = definitionsJObject["value"].Children();
                foreach (var childDefinition in children)
                {
                    JObject childObject = (JObject)childDefinition;
                    if (definition["quality"].ToString() == "definition" &&
                        definition["path"].ToString() == childObject["path"].ToString())
                    {
                        definitions.Add(childObject);
                    }
                }
            }
            return definitions;
        }

        public IEnumerable<JProperty> FindIdentifiableDefinitionProperties(JObject definition)
        {
            return s_InstanceIdentifiableField
                .Select(definition.Property)
                .Where(property => property != null);
        }

        private static string VstsBuildsApiBase(JObject definition)
        {
            return definition["project"]["name"].ToString();
        }

        private static string GetVstsBuildsApiUrl(JObject definition)
        {
            return definition["project"]["url"].ToString();
        }
    }
}
