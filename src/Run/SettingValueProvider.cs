// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Microsoft.DotNet.Execute
{
    internal static class SettingValueProvider
    {
        private static string s_cpuArch = null;
        private static string s_osName = null;
        private static string s_osRid = null;
        private static Dictionary<string, string> s_osToDefaultVersionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { OS.OSX, "osx.10.10" },
            { OS.FreeBSD, "osx.10.10" },
            { OS.NetBSD, "osx.10.10" },
            { OS.Linux, "ubuntu.14.04" },
            { OS.Windows, "win7" }
        };

        public static string Get(string settingName)
        {
            PropertyInfo property = typeof(SettingValueProvider).GetTypeInfo().GetProperty(settingName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static);
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

        public static string ProcessorCount
        {
            get
            {
                return Environment.ProcessorCount.ToString();
            }
        }

        public static string OSRid
        {
            get
            {
                if (s_osRid == null)
                {
                    switch (OSName)
                    {
                        case OS.Linux:
                            if (!Interop.GetUnixVersion(out s_osRid))
                            {
                                s_osRid = s_osToDefaultVersionMap[OS.Linux];
                            }
                            break;

                        default:
                            s_osRid = s_osToDefaultVersionMap[OSName];
                            break;
                    }
                }

                return s_osRid;
            }
        }

        public static string OSName
        {
            get
            {
                if (s_osName == null)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        s_osName = OS.Windows;
                    }
                    else
                    {
                        string[] osValues = new string[] { OS.Linux, OS.FreeBSD, OS.NetBSD, OS.OpenBSD, OS.SunOS, OS.Darwin };
                        string osValue = osValues.FirstOrDefault(s => RuntimeInformation.OSDescription.Contains(s));
                        switch (osValue)
                        {
                            case OS.Darwin:
                                s_osName = OS.OSX;
                                break;

                            case null:
                                s_osName = OS.Linux;
                                break;

                            default:
                                s_osName = osValue;
                                break;
                        }
                    }
                }

                return s_osName;
            }
        }
    }
}
