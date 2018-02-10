// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.VersionTools.BuildManifest
{
    public class SupplementaryUploadRequest
    {
        /// <summary>
        /// Path, relative to the primary upload dir or absolute with a leading '/'.
        /// </summary>
        public string Path { get; set; }

        public string Contents { get; set; }

        public string GetAbsolutePath(string currentPath)
        {
            if (Path.StartsWith("/"))
            {
                return Path.Substring(1);
            }
            return $"{currentPath}/{Path}";
        }
    }
}
