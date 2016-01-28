// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace NuGet
{
    public class ManifestContentFiles
    {
        public string Include { get; set; }
        
        public string Exclude { get; set; }
        
        public string BuildAction { get; set; }

        public string CopyToOutput { get; set; }

        public string Flatten { get; set; }
    }
}