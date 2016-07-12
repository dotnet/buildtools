// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.IO;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.DotNet.Execute
{
    internal static class SettingValueProvider
    {
        private static string s_cpuArch = null;
        private static string s_osName = null;
        private static string s_osVersion = null;
        private static Dictionary<string, string> s_osToDefaultVersionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { OS.OSX, "osx.10.10" },
            { OS.FreeBSD, "osx.10.10" },
            { OS.NetBSD, "osx.10.10" },
            { OS.Linux, "ubuntu.14.04" },
            { OS.Windows, string.Empty }
        };

        public static string Get(string settingName)
        {
            PropertyInfo property = typeof(SettingValueProvider).GetTypeInfo().GetProperty(settingName);
            return (property != null) ? (string)property.GetValue(null) : string.Empty;
        }
        
        public static string CPUArch
        {
            get
            {
                if (s_cpuArch == null)
                {
                    switch (RuntimeInformation.OSArchitecture)
                    {
                        case Architecture.X86:
                            s_cpuArch = Arch.X86;
                            break;

                        case Architecture.Arm:
                            s_cpuArch = Arch.Arm;
                            break;

                        case Architecture.Arm64:
                            s_cpuArch = Arch.Arm64;
                            break;

                        case Architecture.X64:
                        default:
                            s_cpuArch = Arch.X64;
                            break;
                    }
                }

                return s_cpuArch;
            }
        }

        public static string OSVersion
        {
            get
            {
                if (s_osVersion == null)
                {
                    switch (OSName)
                    {
                        case OS.Linux:
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
                                    s_osVersion = $"{id}.{version}";
                                }
                                else
                                {
                                    s_osVersion = s_osToDefaultVersionMap[OS.Linux];
                                }
                            }
                            break;

                        default:
                            s_osVersion = s_osToDefaultVersionMap[OSName];
                            break;
                    }
                }

                return s_osVersion;
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
                        s_osName = OS.OSX;
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")))
                    {
                        s_osName = OS.FreeBSD;
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD")))
                    {
                        s_osName = OS.NetBSD;
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        s_osName = OS.Linux;
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        s_osName = OS.Windows;
                    }
                }

                return s_osName;
            }
        }
    }
}
