// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.VstsBuildsApi
{
    internal class JsonHttpClient
    {
        private HttpClient _client;

        public JsonHttpClient(Uri baseAddress, string credentials)
        {
            _client = new HttpClient
            {
                BaseAddress = baseAddress
            };
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        public async Task<JObject> GetAsync(string address)
        {
            return await SendAsync(HttpMethod.Get, address, null);
        }

        public async Task<JObject> PostAsync(string address, JObject content)
        {
            return await SendAsync(HttpMethod.Post, address, content);
        }

        public async Task<JObject> PutAsync(string address, JObject content)
        {
            return await SendAsync(HttpMethod.Put, address, content);
        }

        private async Task<JObject> SendAsync(HttpMethod method, string address, JObject content)
        {
            var message = new HttpRequestMessage(method, address);
            if (content != null)
            {
                message.Content = CreateJsonHttpContent(content);
            }
            HttpResponseMessage response = await _client.SendAsync(message);
            ProcessResponseStatusCode(response);
            return JObject.Parse(await response.Content.ReadAsStringAsync());
        }

        private static HttpContent CreateJsonHttpContent(JObject content)
        {
            return new StringContent(
                JsonConvert.SerializeObject(content),
                Encoding.UTF8,
                "application/json");
        }

        /// <summary>
        /// This method performs similar functionality as
        /// HttpResponseMessage.EnsureSuccessStatusCode(), but is more strict about what "success"
        /// is because we can not properly handle "non-authoritative" responses which are reported
        /// as "success".
        /// </summary>
        private static void ProcessResponseStatusCode(HttpResponseMessage response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return;
            }
            throw new HttpRequestException(string.Format("Response code {0} received from {1} is not a valid response.", response.StatusCode, response.RequestMessage.RequestUri));
        }
    }
}
