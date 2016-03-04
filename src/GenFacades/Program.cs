// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.MutableCodeModel;
using Microsoft.Fx.CommandLine;

namespace GenFacades
{
    public class Program
    {
        private const uint ReferenceAssemblyFlag = 0x70;

        public static int Main(string[] args)
        {
            string seeds = null;
            string contracts = null;
            string facadePath = null;
            Version assemblyFileVersion = null;
            bool clearBuildAndRevision = false;
            bool ignoreMissingTypes = false;
            bool buildDesignTimeFacades = false;
            string inclusionContracts = null;
            ErrorTreatment seedLoadErrorTreatment = ErrorTreatment.Default;
            ErrorTreatment contractLoadErrorTreatment = ErrorTreatment.Default;
            string[] seedTypePreferencesUnsplit = null;
            bool forceZeroVersionSeeds = false;
            bool producePdb = true;
            string partialFacadeAssemblyPath = null;

            bool parsingSucceeded = CommandLineParser.ParseForConsoleApplication((parser) =>
            {
                parser.DefineQualifier("facadePath", ref facadePath, "Path to output the facades.");
                parser.DefineQualifier("seeds", ref seeds, "Path to the seed assemblies. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineQualifier("contracts", ref contracts, "Path to the contract assemblies. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineOptionalQualifier("assemblyFileVersion", ref assemblyFileVersion, "Override the AssemblyFileVersion attribute from the contract with the given version for the generated facade.");
                parser.DefineOptionalQualifier("clearBuildAndRevision", ref clearBuildAndRevision, "Generate facade assembly version x.y.0.0 for contract version x.y.z.w");
                parser.DefineOptionalQualifier("ignoreMissingTypes", ref ignoreMissingTypes, "Ignore types that cannot be found in the seed assemblies. This is not recommended but is sometimes helpful while hacking around or trying to produce partial facades.");
                parser.DefineOptionalQualifier("designTime", ref buildDesignTimeFacades, "Enable design-time facade generation (marks facades with reference assembly flag and attribute).");
                parser.DefineOptionalQualifier("include", ref inclusionContracts, "Add types from these contracts to the facades. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineOptionalQualifier("seedError", ref seedLoadErrorTreatment, "Error handling for seed assembly load failure.");
                parser.DefineOptionalQualifier("contractError", ref seedLoadErrorTreatment, "Error handling for contract assembly load failure.");
                parser.DefineOptionalQualifier("preferSeedType", ref seedTypePreferencesUnsplit, "Set which seed assembly to choose for a given type when it is defined in more than one assembly. Format: FullTypeName=PreferredSeedAssemblyName");
                parser.DefineOptionalQualifier("forceZeroVersionSeeds", ref forceZeroVersionSeeds, "Forces all seed assembly versions to 0.0.0.0, regardless of their true version.");
                parser.DefineOptionalQualifier("partialFacadeAssemblyPath", ref partialFacadeAssemblyPath, "Specifies the path to a single partial facade assembly, into which appropriate type forwards will be added to satisfy the given contract. If this option is specified, only a single partial assembly and a single contract may be given.");
                parser.DefineOptionalQualifier("producePdb", ref producePdb, "Specifices if a PDB file should be produced for the resulting partial facade.");
            }, args);

            if (!parsingSucceeded)
            {
                return 1;
            }

            CommandLineTraceHandler.Enable();

            try
            {
                Generator.GenerateFacades(
                    seeds, contracts, facadePath, assemblyFileVersion, clearBuildAndRevision, ignoreMissingTypes,
                    buildDesignTimeFacades, inclusionContracts, seedLoadErrorTreatment, contractLoadErrorTreatment,
                    seedTypePreferencesUnsplit, forceZeroVersionSeeds, producePdb, partialFacadeAssemblyPath);

                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error encountered generating facades: " + e.ToString());
                return -1;
            }
        }
    }
}
