// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Build.VstsBuildsApi.Configuration;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.DotNet.Build.VstsBuildsApi
{
    internal class VstsBuildHttpClient : VstsDefinitionHttpClient
    {
        private const string BuildApiType = "builds";

        public VstsBuildHttpClient(JObject definition, VstsApiEndpointConfig config)
            : base(new Uri(definition["project"]["url"].ToString()), config, BuildApiType)
        {
        }

        protected override bool IsMatching(JObject localDefinition, JObject retrievedDefinition)
        {
            return localDefinition["quality"].ToString() == "definition" &&
                localDefinition["path"].ToString() == retrievedDefinition["path"].ToString();
        }

        protected override string GetDefinitionProject(JObject definition)
        {
            return definition["project"]["name"].ToString();
        }
    }
}
