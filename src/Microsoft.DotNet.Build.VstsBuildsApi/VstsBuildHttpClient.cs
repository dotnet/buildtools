// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Build.VstsBuildsApi.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.VstsBuildsApi
{
    internal class VstsBuildHttpClient : VstsDefinitionHttpClient
    {
        private const string BuildApiType = "build";

        public VstsBuildHttpClient(JObject definition, VstsApiEndpointConfig config)
            : base(new Uri(definition["project"]["url"].ToString()), config, BuildApiType)
        {
        }

        protected override bool IsMatching(JObject localDefinition, JObject retrievedDefinition)
        {
            string localDefinitionPath = localDefinition["path"].ToString();

            // If it's not the root directory.
            if (Path.GetDirectoryName(localDefinitionPath) != null) {
                // Remove trailing path separators since VSTS does the same when creating definitions.
                localDefinitionPath = localDefinitionPath.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return localDefinition["quality"].ToString() == "definition" &&
                localDefinitionPath == retrievedDefinition["path"].ToString();
        }

        protected override string GetDefinitionProject(JObject definition)
        {
            return definition["project"]["name"].ToString();
        }
    }
}
