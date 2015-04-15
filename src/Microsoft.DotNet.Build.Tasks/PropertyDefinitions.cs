// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.ContentModel;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.NuGet.Build.Tasks
{
    public class PropertyDefinitions
    {
        public PropertyDefinitions()
        {
            Definitions = new Dictionary<string, ContentPropertyDefinition>
                {
                    { "arch", _arch },
                    { "language", _language },
                    { "tfm", _targetFramework },
                    { "tpm", _targetPlatform },
                    { "assembly", _assembly },
                    { "dynamicLibrary", _dynamicLibrary },
                    { "resources", _resources },
                    { "locale", _locale },
                    { "any", _any },
                };
        }

        public IDictionary<string, ContentPropertyDefinition> Definitions { get; private set; }

        private readonly ContentPropertyDefinition _arch = new ContentPropertyDefinition
        {
            Table =
                {
                    { "x86", "x86" },
                    { "x64", "amd64" },
                    { "amd64", "amd64" },
                    { "arm64", "arm64" },
                    { "universal","anyCpu" },
                }
        };

        private readonly ContentPropertyDefinition _language = new ContentPropertyDefinition
        {
            Table =
                {
                    { "cs", "CSharp" },
                    { "vb", "Visual Basic" },
                    { "fs", "FSharp" },
                }
        };

        private readonly ContentPropertyDefinition _targetFramework = new ContentPropertyDefinition
        {
            Table =
                {
                    { "aspnet50", new NuGetFramework("ASP.NET", new Version(5, 0)) },
                    { "aspnetcore50", new NuGetFramework("ASP.NETCore", new Version(5, 0)) },
                    { "dnxcore50", new NuGetFramework("DNXCore", new Version(5, 0)) },
                    { "any", NuGetFramework.AnyFramework },
                    { "monoandroid", new NuGetFramework("MonoAndroid",new Version()) },
                    { "monotouch", new NuGetFramework("MonoTouch",new Version()) },
                    { "monomac", new NuGetFramework("MonoMac", new Version()) },
                    { "netcore50", new NuGetFramework(".NETCore", new Version(5, 0)) },
                },
            Parser = TargetFrameworkName_Parser,
            OnIsCriteriaSatisfied = TargetFrameworkName_IsCriteriaSatisfied
        };

        private readonly ContentPropertyDefinition _targetPlatform = new ContentPropertyDefinition
        {
            Table =
                {
                    { "win81", new FrameworkName("Windows", new Version(8, 1)) },
                    { "win8", new FrameworkName("Windows", new Version(8, 0)) },
                    { "win7", new FrameworkName("Windows", new Version(7, 0)) },
                    { "windows", new FrameworkName("Windows", new Version(0, 0)) },
                    { "wp8", new FrameworkName("WindowsPhone", new Version(8, 0)) },
                    { "wp81", new FrameworkName("WindowsPhone", new Version(8, 1)) },
                    { "uap10", new FrameworkName("UAP", new Version(10, 0)) },
                    { "darwin", new FrameworkName("Darwin", new Version(0, 0)) },
                },
            OnIsCriteriaSatisfied = TargetPlatformName_IsCriteriaSatisfied,
        };

        private readonly ContentPropertyDefinition _assembly = new ContentPropertyDefinition
        {
            FileExtensions = { ".dll" }
        };

        private readonly ContentPropertyDefinition _dynamicLibrary = new ContentPropertyDefinition
        {
            FileExtensions = { ".dll", ".dylib", ".so" }
        };

        private readonly ContentPropertyDefinition _resources = new ContentPropertyDefinition
        {
            FileExtensions = { ".resources.dll" }
        };

        private readonly ContentPropertyDefinition _locale = new ContentPropertyDefinition
        {
            Parser = Locale_Parser,
        };

        private readonly ContentPropertyDefinition _any = new ContentPropertyDefinition
        {
            Parser = name => name
        };


        internal static object Locale_Parser(string name)
        {
            if (name.Length == 2)
            {
                return name;
            }
            else if (name.Length >= 4 && name[2] == '-')
            {
                return name;
            }

            return null;
        }

        internal static object TargetFrameworkName_Parser(string name)
        {
            if (name.Contains("/"))
            {
                return null;
            }

            var result = NuGetFramework.Parse(name);

            if (result != NuGetFramework.UnsupportedFramework)
            {
                return result;
            }

            return null;
        }

        internal static bool TargetFrameworkName_IsCriteriaSatisfied(object criteria, object available)
        {
            var criteriaFrameworkName = criteria as NuGetFramework;
            var availableFrameworkName = available as NuGetFramework;

            if (criteriaFrameworkName != null && availableFrameworkName != null)
            {
                return DefaultCompatibilityProvider.Instance.IsCompatible(criteriaFrameworkName, availableFrameworkName);
            }

            return false;
        }

        internal static bool TargetPlatformName_IsCriteriaSatisfied(object criteria, object available)
        {
            var criteriaFrameworkName = criteria as FrameworkName;
            var availableFrameworkName = available as FrameworkName;

            if (criteriaFrameworkName != null && availableFrameworkName != null)
            {
                if (!string.Equals(criteriaFrameworkName.Identifier, availableFrameworkName.Identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // HACK: UAP is windows
                if (criteriaFrameworkName.Identifier == "UAP" && availableFrameworkName.Identifier == "Windows")
                {
                    return true;
                }

                if (NormalizeVersion(criteriaFrameworkName.Version) < NormalizeVersion(availableFrameworkName.Version))
                {
                    return false;
                }

                return true;
            }
            return false;
        }

        internal static Version NormalizeVersion(Version version)
        {
            return new Version(version.Major,
                               version.Minor,
                               Math.Max(version.Build, 0),
                               Math.Max(version.Revision, 0));
        }
    }
}
