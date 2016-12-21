// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Build.VstsBuildsApi.Configuration
{
    /// <summary>
    /// Configuration for VSTS Definition client endpoints.
    /// <see cref="VstsDefinitionClient.CreateDefaultConfig"/> creates a default configuration,
    /// otherwise this can be created manually or deserialized from a file.
    /// </summary>
    public class VstsDefinitionClientConfig
    {
        public VstsApiEndpointConfig BuildDefinitionEndpointConfig { get; set; }

        public VstsApiEndpointConfig ReleaseDefinitionEndpointConfig { get; set; }
    }
}
