// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    internal class PackageMetadata
    {
        public string Name = null;  // Remove warnings
        public string Description = null;
        public string[] CommonTypes = null;

        public static IEnumerable<PackageMetadata> ReadFrom(string path)
        {
            string packageMetadata = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<List<PackageMetadata>>(packageMetadata);
        }
    }
}
