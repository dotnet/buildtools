// Licensed to the .NET Foundation under one or more agreements. 
// The .NET Foundation licenses this file to you under the MIT license. 
// See the LICENSE file in the project root for more information. 

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace VstsBuildsApi
{
    public class VstsBuildClient
    {
        private string _collectionIdentifier;
        private string _credentials;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="collectionIdentifier">collection identifer for build definitions</param>
        /// <param name="credentials">credentials used for basic rest api authentication</param>
        public VstsBuildClient(string collectionIdentifier, string credentials)
        {
            if(string.IsNullOrWhiteSpace(collectionIdentifier))
            {
                throw new Exception("Required parameter (collectionIdentifier) cannot be null or white space.");
            }
            if(!Uri.IsWellFormedUriString(collectionIdentifier, UriKind.Relative))
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
        /// <returns>Created or updated build Id</returns>
        public async Task<string> CreateOrUpdateDefinitionAsync(Stream stream)
        {
            JObject definition = GetDefinition(stream);
            return await CreateOrUpdateDefinitionAsync(definition).ConfigureAwait(false);
        }

        /// <summary>
        /// Load a local build definition, combine the uniqueIdentifier with the definition name, 
        /// check VSTS for a matching definition name, if present, update that definition with the
        /// local build definition, otherwise create a new definition.
        /// </summary>
        /// <param name="definition">VSTS build definition JSON object</param>
        /// <returns>Created or updated build definition id</returns>
        private async Task<string> CreateOrUpdateDefinitionAsync(JObject definition)
        {
            var key = string.Join("_", _collectionIdentifier, definition["name"].ToString());
            definition["name"] = key;
            IReadOnlyList<JObject> vstsDefinitions = await VstsRetrieveDefinitionsListByNameAndPathAsync(definition).ConfigureAwait(false);

            if (vstsDefinitions.Count == 0)
            {
                /* Create */
                JObject vstsDefinition = await VstsCreateDefinitionAsync(definition).ConfigureAwait(false);
                return vstsDefinition["id"].ToString();
            }
            else if(vstsDefinitions.Count == 1)
            {
                JObject vstsDefinition = await VstsRetrieveDefinitionByIdAsync(vstsDefinitions[0]).ConfigureAwait(false);

                /* Update */
                if (!IsSameContents(definition, vstsDefinition))
                {
                    definition["revision"] = vstsDefinition["revision"];
                    definition["id"] = vstsDefinition["id"];
                    definition["uri"] = vstsDefinition["uri"];
                    definition["path"] = vstsDefinition["path"];
                    definition["createdDate"] = vstsDefinition["createdDate"];
                    definition["url"] = vstsDefinition["url"];
                    definition["comment"] = "Automated update";

                    vstsDefinition = await VstsUpdateDefinitionAsync(definition).ConfigureAwait(false);
                }
                return vstsDefinition["id"].ToString();
            }
            else
            {
                throw new Exception(string.Format("Obtained multiple {0} definitions with the same 'name' ({1}) and 'path' ({2}) properties.  This should not be possible.", vstsDefinitions.Count, vstsDefinitions[0]["name"].ToString(), vstsDefinitions[0]["path"].ToString()));
            }
        }

        private JObject GetDefinition(Stream stream)
        {
            using (StreamReader streamReader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
            {
               return (JObject)JToken.ReadFrom(jsonReader);
            }
        }

        /// <summary>
        /// Update VSTS Build Definition
        /// </summary>
        /// <returns>updated definitions id</returns>
        private async Task<JObject> VstsUpdateDefinitionAsync(JObject definition)
        {
            string requestUri = $"DefaultCollection/{VstsBuildsApiBase(definition)}/_apis/build/definitions/{definition["id"].ToString()}?api-version=2.0";

            HttpContent content = new StringContent(JsonConvert.SerializeObject(definition), System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await GetClient(definition["url"].ToString()).PutAsync(requestUri, content);
            ProcessResponseStatusCode(response);
            return JObject.Parse(await response.Content.ReadAsStringAsync());
        }

        /// <summary>
        /// Create VSTS Build Definition
        /// </summary>
        private async Task<JObject> VstsCreateDefinitionAsync(JObject definition)
        {
            /* Remove definition instance identifiable information */
            RemoveJObjectToken("revison", definition);
            RemoveJObjectToken("uri", definition);
            RemoveJObjectToken("createdDate", definition);
            RemoveJObjectToken("comment", definition);
            RemoveJObjectToken("_links", definition);
            RemoveJObjectToken("authoredBy", definition);
            RemoveJObjectToken("id", definition);

            string requestUri = $"DefaultCollection/{VstsBuildsApiBase(definition)}/_apis/build/definitions?api-version=2.0";

            HttpContent content = new StringContent(JsonConvert.SerializeObject(definition), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await GetClient(definition["url"].ToString()).PostAsync(requestUri, content);
            ProcessResponseStatusCode(response);
            return JObject.Parse(await response.Content.ReadAsStringAsync());

        }

        /// <summary>
        /// Retrieve a VSTS Build Definition by name and VSTS folder path
        /// </summary>
        private async Task<JObject> VstsRetrieveDefinitionByIdAsync(JObject definition)
        {
            string requestUri = $"DefaultCollection/{VstsBuildsApiBase(definition)}/_apis/build/definitions/{definition["id"].ToString()}?api-version=2.0";

            HttpResponseMessage response = await GetClient(definition["url"].ToString()).GetAsync(requestUri);
            ProcessResponseStatusCode(response);
            return JObject.Parse(await response.Content.ReadAsStringAsync());
        }

        /// <summary>
        /// Retrieve a VSTS Build Definition by name and VSTS folder path
        /// </summary>
        private async Task<IReadOnlyList<JObject>> VstsRetrieveDefinitionsListByNameAndPathAsync(JObject definition)
        {
            string requestUri = $"DefaultCollection/{VstsBuildsApiBase(definition)}/_apis/build/definitions?api-version=2.0&name={definition["name"].ToString()}";

            HttpResponseMessage response = await GetClient(definition["url"].ToString()).GetAsync(requestUri);
            ProcessResponseStatusCode(response);
            string json = await response.Content.ReadAsStringAsync();
            JObject definitionsJObject = JObject.Parse(json);
            List<JObject> definitions = new List<JObject>();
            if (int.Parse(definitionsJObject["count"].Value<string>()) > 0)
            {
                var children = definitionsJObject["value"].Children();
                foreach(var childDefinition in children)
                {
                    JObject childObject = (JObject)childDefinition;
                    if (definition["path"].ToString() == childObject["path"].ToString())
                    {
                        definitions.Add(childObject);
                    }
                }
            }
            return definitions;
        }

        private string VstsBuildsApiBase(JObject definition)
        {
            return definition["project"]["name"].ToString();
        }

        private HttpClient GetClient(string url)
        {
            HttpClient client = new HttpClient();
            Uri uri = new Uri(url); 
            client.BaseAddress = new Uri(string.Format("{0}://{1}/", uri.Scheme, uri.Authority));
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _credentials);
            return client;
        }

        // This method performs similar functionality as HttpResponseMessage.EnsureSuccessStatusCode(), but is more strict about 
        // what "success" is because we can not properly handle "non-authoratative" responses which are reported as "success".
        private void ProcessResponseStatusCode(HttpResponseMessage response)
        {
            if(response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return;
            }
            throw new HttpRequestException(string.Format("Response code {0} received from {1} is not a valid response.", response.StatusCode, response.RequestMessage.RequestUri));
        }

        private static bool IsSameContents(JObject definition1, JObject definition2)
        {
            JObject _definition1 = (JObject)definition1.DeepClone();
            JObject _definition2 = (JObject)definition2.DeepClone();

            RemoveJObjectToken("_links", _definition1, _definition2);
            RemoveJObjectToken("project", _definition1, _definition2);
            RemoveJObjectToken("comment", _definition1, _definition2);
            RemoveJObjectToken("revision", _definition1, _definition2);
            RemoveJObjectToken("id", _definition1, _definition2);
            RemoveJObjectToken("uri", _definition1, _definition2);
            RemoveJObjectToken("path", _definition1, _definition2);
            RemoveJObjectToken("createdDate", _definition1, _definition2);
            RemoveJObjectToken("url", _definition1, _definition2);

            return JToken.DeepEquals(_definition1, _definition2);
        }

        private static void RemoveJObjectToken(string tokenName, params JObject[] jObjects)
        {
            foreach(var jObject in jObjects)
            {
                jObject.Remove(tokenName);
            }
        }
    }
}
