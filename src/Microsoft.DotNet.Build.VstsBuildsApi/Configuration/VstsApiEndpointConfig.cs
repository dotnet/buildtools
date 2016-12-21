// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Build.VstsBuildsApi.Configuration
{
    /// <summary>
    /// Configuration for a VSTS REST API endpoint.
    /// </summary>
    public class VstsApiEndpointConfig
    {
        /// <summary>
        /// The basic auth PAT credentials used to access this endpoint.
        /// </summary>
        public string Credentials { get; set; }

        /// <summary>
        /// The API version to use, e.g. "2.0".
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// Fields retrieved through this API that make the definition identifiable to this VSTS
        /// instance. These may need to be removed, added, or copied when determining the correct
        /// behavior to ensure the definition is up to date.
        /// </summary>
        public string[] InstanceIdentifiableFields { get; set; }
    }
}
