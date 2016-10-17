// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Compose.Model.Command
{
    public class ConsumesOutput : SortedDictionary<string, ConsumesPlatform>
    {
        public const string OsAllName = "os-all";

        public ConsumesPlatform OsAll
        {
            get { return this[OsAllName]; }
            set { this[OsAllName] = value; }
        }
    }
}
