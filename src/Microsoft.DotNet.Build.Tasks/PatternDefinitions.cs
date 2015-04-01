// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.ContentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NuGet.Build.Tasks
{
    internal class PatternDefinitions
    {
        public PropertyDefinitions Properties { get; private set; }
        public ContentPatternDefinition CompileTimeAssemblies { get; private set; }
        public ContentPatternDefinition ManagedAssemblies { get; private set; }
        public ContentPatternDefinition AheadOfTimeAssemblies { get; private set; }
        public ContentPatternDefinition ResourceAssemblies { get; private set; }
        public ContentPatternDefinition NativeLibraries { get; private set; }

        public PatternDefinitions()
        {
            Properties = new PropertyDefinitions();

            ManagedAssemblies = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "lib/{tfm}~{tpm}/{any?}",
                    "lib/{tfm}/{any?}",
                },
                PathPatterns =
                {
                    "lib/{tfm}~{tpm}/{assembly}",
                    "lib/{tfm}/{assembly}",
                },
                PropertyDefinitions = Properties.Definitions,
            };

            CompileTimeAssemblies = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "ref/{tfm}~{tpm}/{any?}",
                    "ref/{tfm}/{any?}",
                },
                PathPatterns =
                {
                    "ref/{tfm}~{tpm}/{assembly}",
                    "ref/{tfm}/{assembly}",
                },
                PropertyDefinitions = Properties.Definitions,
            };

            AheadOfTimeAssemblies = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "aot/{tfm}~{tpm}/{any?}",
                    "aot/{tfm}/{any?}",
                },
                PathPatterns =
                {
                    "aot/{tfm}~{tpm}/{assembly}",
                    "aot/{tfm}/{assembly}",
                },
                PropertyDefinitions = Properties.Definitions,
            };

            ResourceAssemblies = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "lib/{tfm}~{tpm}/{locale?}/{resources?}",
                    "lib/{tfm}/{locale?}/{resources?}",
                },
                PathPatterns =
                {
                    "lib/{tfm}.{tpm}/{locale}/{resources}",
                    "lib/{tfm}/{locale}/{resources}",
                },
                PropertyDefinitions = Properties.Definitions,
            };

            NativeLibraries = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "lib/{tfm}~{tpm}/{arch}/{any?}",
                    "lib/{tpm}/{arch}/{any?}",
                },
                PathPatterns =
                {
                    "lib/{tfm}~{tpm}/{arch}/{dynamicLibrary}",
                    "lib/{tpm}/{arch}/{dynamicLibrary}",
                },
                PropertyDefinitions = Properties.Definitions,
            };
        }
    }
}
