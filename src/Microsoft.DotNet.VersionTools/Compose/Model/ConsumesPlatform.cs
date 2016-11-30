// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Compose.Model
{
    public class ConsumesPlatform
    {
        [JsonProperty("dependencies")]
        public SortedDictionary<string, ArtifactSet> Dependencies { get; set; }
    }
}