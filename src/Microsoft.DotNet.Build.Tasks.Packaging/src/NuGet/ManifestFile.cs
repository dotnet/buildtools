// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace NuGet
{
    public class ManifestFile
    {
        public ManifestFile(string source, string target, string exclude)
        {
            Source = source;
            Target = target;
            Exclude = exclude;
        }

        public string Source { get; }
        
        public string Target { get; }

        public string Exclude { get; }
    }
}
