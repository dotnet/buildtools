// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.IO;
using System;

namespace Microsoft.DotNet.Execute
{
    internal class SettingValueProvider
    {
        private static string s_cpuArch = null;
        private static string s_osName = null;

        public static string CPUArch
        {
            get
            {
                if (s_cpuArch == null)
                {
                    switch (RuntimeInformation.OSArchitecture)
                    {
                        case Architecture.X86:
                            s_cpuArch = "x86";
                            break;

                        case Architecture.X64:
                            s_cpuArch = "x64";
                            break;

                        case Architecture.Arm:
                            s_cpuArch = "arm";
                            break;

                        case Architecture.Arm64:
                            s_cpuArch = "arm64";
                            break;

                        default:
                            s_cpuArch = "x64";
                            break;
                    }
                }

                return s_cpuArch;
            }
        }

        public static string OSName
        {
            get
            {
                if (s_osName == null)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        s_osName = "OSX";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")))
                    {
                        s_osName = "FreeBSD";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD")))
                    {
                        s_osName = "NetBSD";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
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
                                s_osName = $"{id}.{version}";
                            }
                            else
                            {
                                s_osName = "ubuntu.14.04";
                            }
                        }
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        s_osName = "Windows_NT";
                    }
                }

                return s_osName;
            }
        }
    }
}
