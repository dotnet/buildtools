// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.Execute
{
    internal class Interop
    {
        public static bool GetUnixVersion(out string result)
        {
            const string OSId = "ID=";
            const string OSVersionId = "VERSION_ID=";
            result = null;
            const string OSReleaseFileName = @"/etc/os-release";
            if (File.Exists(OSReleaseFileName))
            {
                string[] content = File.ReadAllLines(OSReleaseFileName);
                string id = null, version = null;
                foreach (string line in content)
                {
                    if (line.StartsWith(OSId))
                    {
                        int startOfVersion = line.IndexOf('"', OSId.Length) + 1;
                        int endOfVersion = startOfVersion == 0 ? line.Length : line.IndexOf('"', startOfVersion);
                        if (startOfVersion == 0)
                            startOfVersion = OSId.Length;

                        id = line.Substring(startOfVersion, endOfVersion - startOfVersion);
                    }
                    else if (line.StartsWith(OSVersionId))
                    {
                        int startOfVersion = line.IndexOf('"', OSVersionId.Length) + 1;
                        int endOfVersion = startOfVersion == 0 ? line.Length : line.IndexOf('"', startOfVersion);
                        if (startOfVersion == 0)
                            startOfVersion = OSVersionId.Length;

                        version = line.Substring(startOfVersion, endOfVersion - startOfVersion);
                    }

                    // Skip parsing rest of the file contents.
                    if (id != null && version != null)
                        break;
                }

                if (id.Equals("rhel", StringComparison.OrdinalIgnoreCase))
                {
                    // For rhel just return the major version number.
                    version = version.Substring(0, version.IndexOf('.'));
                }

                result = $"{id}.{version}";
            }

            return result != null;
        }
    }
}
