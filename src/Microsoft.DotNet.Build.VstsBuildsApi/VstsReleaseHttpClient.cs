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
    internal class VstsReleaseHttpClient : IVstsDefinitionHttpClient
    {
        private static readonly string[] s_InstanceIdentifiableField = {
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
        };

        private const string ApiVersion = "3.1-preview.3";

        private JsonHttpClient _jsonClient;

        public VstsReleaseHttpClient(JObject definition, string credentials)
        {
            Uri uri = new Uri(GetVstsReleaseApiUrl(definition));

            _jsonClient = new JsonHttpClient(
                new Uri(string.Format("{0}://{1}/", uri.Scheme, uri.Authority)),
                credentials);
        }

        public async Task<JObject> VstsUpdateDefinitionAsync(JObject definition)
        {
            string requestUri = GetRequestUri(definition, "definitions");
            return await _jsonClient.PutAsync(requestUri, definition);
        }

        public async Task<JObject> VstsCreateDefinitionAsync(JObject definition)
        {
            string requestUri = GetRequestUri(definition, "definitions");
            return await _jsonClient.PostAsync(requestUri, definition);
        }

        public async Task<JObject> VstsRetrieveDefinitionByIdAsync(JObject definition)
        {
            string requestUri = GetRequestUri(definition, $"definitions/{definition["id"]}");
            return await _jsonClient.GetAsync(requestUri);
        }

        public async Task<IReadOnlyList<JObject>> VstsRetrieveDefinitionsListByNameAndPathAsync(JObject definition)
        {
            var args = new Dictionary<string, string>
            {
                { "name", definition["name"].Value<string>()}
            };
            string requestUri = GetRequestUri(definition, "definitions", args);

            JObject definitionsJObject = await _jsonClient.GetAsync(requestUri);
            List<JObject> definitions = new List<JObject>();
            if (int.Parse(definitionsJObject["count"].Value<string>()) > 0)
            {
                var children = definitionsJObject["value"].Children();
                foreach (var childDefinition in children)
                {
                    JObject childObject = (JObject)childDefinition;
                    if (definition["name"].ToString() == childObject["name"].ToString())
                    {
                        definitions.Add((JObject)childDefinition);
                    }
                }
            }
            return definitions;
        }

        public IEnumerable<JProperty> FindIdentifiableDefinitionProperties(JObject definition)
        {
            var properties = new List<JProperty>();
            properties.AddRange(FindIdentifiableObjectProperties(definition));

            var environments = definition["environments"].Children<JObject>();
            properties.AddRange(environments.SelectMany(FindIdentifiableObjectProperties));

            var approvals = environments
                .SelectMany(env => new[] { env["preDeployApprovals"], env["postDeployApprovals"] })
                .SelectMany(approvalWrapper => approvalWrapper["approvals"].Values<JObject>());
            properties.AddRange(approvals.SelectMany(FindIdentifiableObjectProperties));

            return properties;
        }

        private static IEnumerable<JProperty> FindIdentifiableObjectProperties(JObject obj)
        {
            return s_InstanceIdentifiableField
                .Select(obj.Property)
                .Where(property => property != null);
        }

        private static string GetRequestUri(JObject definition, string command, Dictionary<string, string> args = null)
        {
            if (args == null)
            {
                args = new Dictionary<string, string>();
            }
            args["api-version"] = ApiVersion;
            string argsQuery = string.Join("&", args.Select(arg => $"{arg.Key}={Uri.EscapeDataString(arg.Value)}"));

            return $"DefaultCollection/{VstsReleaseProject(definition)}/_apis/release/{command}?{argsQuery}";
        }

        /// <summary>
        /// From a url like https://devdiv.vsrm.visualstudio.com/1234/_apis/Release/definitions/1
        /// in the url property of the given definition, gets the project, "1234".
        /// </summary>
        private static string VstsReleaseProject(JObject definition)
        {
            return new Uri(definition["url"].ToString()).Segments[1].TrimEnd('/');
        }

        private static string GetVstsReleaseApiUrl(JObject definition)
        {
            return definition["url"].ToString();
        }
    }
}
