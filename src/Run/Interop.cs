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
            result = null;
            const string OSReleaseFileName = @"/etc/os-release";
            if (File.Exists(OSReleaseFileName))
            {
                string content = File.ReadAllText(OSReleaseFileName);
                int idIndex = content.IndexOf("ID");
                int versionIndex = content.IndexOf("VERSION_ID");
                if (idIndex != -1 && versionIndex != -1)
                {
                    string id = content.Substring(idIndex + 3, content.IndexOf(Environment.NewLine, idIndex + 3) - idIndex - 3);
                    string version = content.Substring(versionIndex + 12, content.IndexOf('"', versionIndex + 12) - versionIndex - 12);
                    result = $"{id}.{version}";
                }
            }

            return result != null;
        }
    }
}
