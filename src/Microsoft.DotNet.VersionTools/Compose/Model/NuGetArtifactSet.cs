// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Compose.Model
{
    public class NuGetArtifactSet
    {
        [JsonProperty("packages")]
        public SortedDictionary<string, PackageVersionList> Packages { get; set; }

        [JsonProperty("release-labels")]
        public SortedDictionary<string, string> ReleaseLabels { get; set; }

        [JsonProperty("feeds")]
        public SortedDictionary<string, string> Feeds { get; set; }
    }
}