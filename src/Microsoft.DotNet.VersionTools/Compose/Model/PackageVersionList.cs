// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Compose.Model
{
    /// <summary>
    /// A list of strings with special Json.NET serialization: when there is only one value, it is
    /// treated as a value, not an array.
    /// </summary>
    [JsonConverter(typeof(ListOrSingleConverter))]
    public class PackageVersionList : List<string>
    {
    }
}