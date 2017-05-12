// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.DotNet.Build.VstsBuildsApi.Configuration
{
    /// <summary>
    /// Configuration for VSTS Definition client endpoints.
    /// <see cref="VstsDefinitionClient.CreateDefaultConfig"/> creates a default configuration,
    /// otherwise this can be created manually or deserialized from a file.
    /// </summary>
    public class VstsDefinitionClientConfig
    {
        private static string[] invalidBuildIdentifiableFields =
            {
                /* If included in the list of identifiable fields, it would be
                 * excluded from create requests, which would prevent VSTS from
                 * saving the build definition at the correct path.
                 */
                "path",
            };

        private VstsApiEndpointConfig buildDefinitionEndpointConfig;

        public VstsApiEndpointConfig BuildDefinitionEndpointConfig
        {
            get
            {
                return buildDefinitionEndpointConfig;
            }
            set
            {
                if (value.InstanceIdentifiableFields.Intersect(invalidBuildIdentifiableFields).Any())
                {
                    throw new ArgumentException(
                        $"{nameof(BuildDefinitionEndpointConfig.InstanceIdentifiableFields)} cannot contain any" +
                        $" of the following fields: {string.Join(",", invalidBuildIdentifiableFields)}");
                }

                buildDefinitionEndpointConfig = value;
            }
        }

        public VstsApiEndpointConfig ReleaseDefinitionEndpointConfig { get; set; }
    }
}
