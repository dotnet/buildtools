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
    internal class VstsReleaseHttpClient : VstsDefinitionHttpClient
    {
        public VstsReleaseHttpClient(JObject definition, VstsApiEndpointConfig config)
            : base(new Uri(definition["url"].ToString()), config)
        {
        }

        public override async Task<JObject> UpdateDefinitionAsync(JObject definition) =>
            await JsonClient.PutAsync(
                GetRequestUri(definition, "definitions"),
                definition);

        protected override string ApiType => "release";

        protected override bool IsMatching(JObject localDefinition, JObject retrievedDefinition)
        {
            return localDefinition["name"].ToString() == retrievedDefinition["name"].ToString();
        }

        protected override IEnumerable<JObject> FindObjectsWithIdentifiableProperties(JObject definition)
        {
            IEnumerable<JObject> environments = definition["environments"].Children<JObject>();

            IEnumerable<JObject> approvals = environments
                .SelectMany(env => new[] { env["preDeployApprovals"], env["postDeployApprovals"] })
                .SelectMany(approvalWrapper => approvalWrapper["approvals"].Values<JObject>());

            return new[] { definition }
                .Concat(environments)
                .Concat(approvals);
        }

        /// <summary>
        /// From a url like https://devdiv.vsrm.visualstudio.com/1234/_apis/Release/definitions/1
        /// in the url property of the given definition, gets the project, "1234".
        /// </summary>
        protected override string GetDefinitionProject(JObject definition)
        {
            return new Uri(definition["url"].ToString()).Segments[1].TrimEnd('/');
        }
    }
}
