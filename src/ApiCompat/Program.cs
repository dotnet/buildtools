using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Cci;
using Microsoft.Cci.Comparers;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Mappings;
using Microsoft.Cci.Writers;
using Microsoft.Cci.Writers.Syntax;
using Microsoft.Fx.CommandLine;

namespace ApiCompat
{
    internal class Program
    {
        private const int _baselineNotFoundError = 3;

        private static void Main(string[] args)
        {
            ParseCommandLine();
            CommandLineTraceHandler.Enable();

            if (_listRules)
            {
                var c = GetCompositionContainer();
                c.ComposeExportedValue<IEqualityComparer<ITypeReference>>(CciComparers.Default.GetEqualityComparer<ITypeReference>());

                var rules = c.GetExportedValues<IDifferenceRule>();

                foreach (var rule in rules.Select(r => r.GetType().Name).OrderBy(r => r))
                {
                    Console.WriteLine(rule);
                }

                return;
            }

            using (TextWriter output = GetOutput())
            {
                if (Environment.ExitCode != 0)
                    return;

                if (output != Console.Out)
                    Trace.Listeners.Add(new TextWriterTraceListener(output) { Filter = new EventTypeFilter(SourceLevels.Error | SourceLevels.Warning) });

                BaselineDifferenceFilter filter = GetBaselineDifferenceFilter();
                NameTable sharedNameTable = new NameTable();
                HostEnvironment contractHost = new HostEnvironment(sharedNameTable);
                contractHost.UnableToResolve += new EventHandler<UnresolvedReference<IUnit, AssemblyIdentity>>(contractHost_UnableToResolve);
                contractHost.ResolveAgainstRunningFramework = _resolveFx;
                contractHost.UnifyToLibPath = _unifyToLibPaths;
                contractHost.AddLibPaths(HostEnvironment.SplitPaths(_contractLibDirs));
                IEnumerable<IAssembly> contractAssemblies = contractHost.LoadAssemblies(_contractSet, _contractCoreAssembly, (message, errorLevel) =>
                    {
                        if (filter != null && filter.Include(message))
                            contractHost.TraceErrorWithLevel(errorLevel, message);
                    });

                if (_ignoreDesignTimeFacades)
                    contractAssemblies = contractAssemblies.Where(a => !a.IsFacade());

                HostEnvironment implHost = new HostEnvironment(sharedNameTable);
                implHost.UnableToResolve += new EventHandler<UnresolvedReference<IUnit, AssemblyIdentity>>(implHost_UnableToResolve);
                implHost.ResolveAgainstRunningFramework = _resolveFx;
                implHost.UnifyToLibPath = _unifyToLibPaths;
                implHost.AddLibPaths(HostEnvironment.SplitPaths(_implDirs));
                if (_warnOnMissingAssemblies)
                    implHost.LoadErrorTreatment = ErrorTreatment.TreatAsWarning;

                // The list of contractAssemblies already has the core assembly as the first one (if _contractCoreAssembly was specified).
                IEnumerable<IAssembly> implAssemblies = implHost.LoadAssemblies(contractAssemblies.Select(a => a.AssemblyIdentity), _warnOnIncorrectVersion, (message, errorLevel) =>
                    {
                        if (filter != null && filter.Include(message))
                            implHost.TraceErrorWithLevel(errorLevel, message);
                    });

                // Exit after loading if the code is set to non-zero
                if (Environment.ExitCode != 0)
                    return;

                ICciDifferenceWriter writer = GetDifferenceWriter(output, filter);
                writer.Write(_implDirs, implAssemblies, _contractSet, contractAssemblies);
            }

            // Note the ExitCode may be set by some of the difference writers
        }

        private static BaselineDifferenceFilter GetBaselineDifferenceFilter()
        {
            BaselineDifferenceFilter filter = null;
            if (!string.IsNullOrEmpty(_baselineFileName))
            {
                if (!File.Exists(_baselineFileName))
                {
                    Console.WriteLine("ERROR: Baseline file {0} was not found!", _baselineFileName);
                    Environment.Exit(_baselineNotFoundError);
                }
                IDifferenceFilter incompatibleFilter = new DifferenceFilter<IncompatibleDifference>();
                filter = new BaselineDifferenceFilter(incompatibleFilter, _baselineFileName);
            }
            return filter;
        }

        static void implHost_UnableToResolve(object sender, UnresolvedReference<IUnit, AssemblyIdentity> e)
        {
            Trace.TraceError("Unable to resolve assembly '{0}' referenced by the implementation assembly '{1}'.", e.Unresolved, e.Referrer);
        }

        static void contractHost_UnableToResolve(object sender, UnresolvedReference<IUnit, AssemblyIdentity> e)
        {
            Trace.TraceError("Unable to resolve assembly '{0}' referenced by the contract assembly '{1}'.", e.Unresolved, e.Referrer);
        }

        private static TextWriter GetOutput()
        {
            if (string.IsNullOrWhiteSpace(_outFile))
                return Console.Out;

            const int NumRetries = 10;
            String exceptionMessage = null;
            for (int retries = 0; retries < NumRetries; retries++)
            {
                try
                {
                    return new StreamWriter(_outFile);
                }
                catch (Exception e)
                {
                    exceptionMessage = e.Message;
                    System.Threading.Thread.Sleep(100);
                }
            }

            Trace.TraceError("Cannot open output file '{0}': {1}", _outFile, exceptionMessage);
            return Console.Out;
        }

        private static ICciDifferenceWriter GetDifferenceWriter(TextWriter writer, IDifferenceFilter filter)
        {
            CompositionContainer container = GetCompositionContainer();

            Func<IDifferenceRuleMetadata, bool> ruleFilter =
                delegate(IDifferenceRuleMetadata ruleMetadata)
                {
                    if (ruleMetadata.MdilServicingRule && !_mdil)
                        return false;
                    return true;
                };

            if (_mdil && _excludeNonBrowsable)
            {
                Trace.TraceWarning("Enforcing MDIL servicing rules and exclusion of non-browsable types are both enabled, but they are not compatible so non-browsable types will not be excluded.");
            }

            MappingSettings settings = new MappingSettings();
            settings.Comparers = GetComparers();
            settings.Filter = GetCciFilter(_mdil, _excludeNonBrowsable);
            settings.DiffFilter = GetDiffFilter(settings.Filter);
            settings.DiffFactory = new ElementDifferenceFactory(container, ruleFilter);
            settings.GroupByAssembly = _groupByAssembly;
            settings.IncludeForwardedTypes = true;

            if (filter == null)
            {
                filter = new DifferenceFilter<IncompatibleDifference>();
            }

            ICciDifferenceWriter diffWriter = new DifferenceWriter(writer, settings, filter);

            container.ComposeExportedValue<IEqualityComparer<ITypeReference>>(settings.TypeComparer);

            // Always compose the diff writer to allow it to import or provide exports
            container.ComposeParts(diffWriter);

            return diffWriter;
        }

        private static CompositionContainer GetCompositionContainer()
        {
            AggregateCatalog catalog = new AggregateCatalog(
                new AssemblyCatalog(typeof(Program).Assembly)
                );
            return new CompositionContainer(catalog);
        }

        private static ICciComparers GetComparers()
        {
            if (!string.IsNullOrEmpty(_remapFile))
            {
                if (!File.Exists(_remapFile))
                {
                    Console.WriteLine("ERROR: RemapFile {0} was not found!", _remapFile);
                    Environment.Exit(1);
                }
                return new NamespaceRemappingComparers(_remapFile);
            }
            return CciComparers.Default;
        }

        private static ICciFilter GetCciFilter(bool enforcingMdilRules, bool excludeNonBrowsable)
        {
            if (enforcingMdilRules)
            {
                return new MdilPublicOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
            else if (excludeNonBrowsable)
            {
                return new PublicEditorBrowsableOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
            else
            {
                return new PublicOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
        }

        private static IMappingDifferenceFilter GetDiffFilter(ICciFilter filter)
        {
            return new MappingDifferenceFilter(GetIncludeFilter(), filter);
        }

        private static Func<DifferenceType, bool> GetIncludeFilter()
        {
            return d => d != DifferenceType.Unchanged;
        }

        private static void ParseCommandLine()
        {
            CommandLineParser p1 = new CommandLineParser();
            p1.DefineOptionalQualifier("listRules", ref _listRules, "Outputs all the rules. If this options is supplied all other options are ignored but you must specify contracts and implDir still '/listRules \"\" /implDirs='.");

            if (_listRules)
                return;

            CommandLineParser.ParseForConsoleApplication(delegate(CommandLineParser parser)
            {
                parser.DefineOptionalQualifier("listRules", ref _listRules, "Outputs all the rules. If this options is supplied all other options are ignored.");
                parser.DefineAliases("baseline", "bl");
                parser.DefineOptionalQualifier("baseline", ref _baselineFileName, "Baseline file to skip known diffs.");
                parser.DefineOptionalQualifier("remapFile", ref _remapFile, "File with a list of type and/or namespace remappings to consider apply to names while diffing.");
                parser.DefineOptionalQualifier("groupByAssembly", ref _groupByAssembly, "Group the differences by assembly instead of flattening the namespaces. Defaults to true.");
                parser.DefineOptionalQualifier("unifyToLibPath", ref _unifyToLibPaths, "Unify the assembly references to the loaded assemblies and the assemblies found in the given directories (contractDepends and implDirs). Defaults to true.");
                parser.DefineOptionalQualifier("out", ref _outFile, "Output file path. Default is the console.");
                parser.DefineOptionalQualifier("resolveFx", ref _resolveFx, "If a contract or implementation dependency cannot be found in the given directories, fallback to try to resolve against the framework directory on the machine.");
                parser.DefineOptionalQualifier("contractDepends", ref _contractLibDirs, "Comma delimited list of directories used to resolve the dependencies of the contract assemblies.");
                parser.DefineAliases("contractCoreAssembly", "cca");
                parser.DefineOptionalQualifier("contractCoreAssembly", ref _contractCoreAssembly, "Simple name for the core assembly to use.");
                parser.DefineAliases("ignoreDesignTimeFacades", "idtf");
                parser.DefineOptionalQualifier("ignoreDesignTimeFacades", ref _ignoreDesignTimeFacades, "Ignore design time facades in the contract set while analyzing.");
                parser.DefineOptionalQualifier("warnOnIncorrectVersion", ref _warnOnIncorrectVersion, "Warn if the contract version number doesn't match the found implementation version number.");
                parser.DefineOptionalQualifier("warnOnMissingAssemblies", ref _warnOnMissingAssemblies, "Warn if the contract assembly cannot be found in the implementation directories. Default is to error and not do anlysis.");
                parser.DefineQualifier("implDirs", ref _implDirs, "Comma delimited list of directories to find the implementation assemblies for each contract assembly.");
                parser.DefineOptionalQualifier("mdil", ref _mdil, "Enforce MDIL servicing rules in addition to IL rules.");
                parser.DefineAliases("excludeNonBrowsable", "enb");
                parser.DefineOptionalQualifier("excludeNonBrowsable", ref _excludeNonBrowsable, "When MDIL servicing rules are not being enforced, exclude validation on types that are marked with EditorBrowsable(EditorBrowsableState.Never).");
                parser.DefineParameter<string>("contracts", ref _contractSet, "Comma delimited list of assemblies or directories of assemblies for all the contract assemblies.");
            });
        }

        private static string _contractCoreAssembly;
        private static string _contractSet;
        private static string _implDirs;
        private static string _contractLibDirs;
        private static bool _listRules;
        private static string _outFile;
        private static string _baselineFileName;
        private static string _remapFile;
        private static bool _groupByAssembly = true;
        private static bool _mdil;
        private static bool _resolveFx;
        private static bool _unifyToLibPaths = true;
        private static bool _warnOnIncorrectVersion;
        private static bool _ignoreDesignTimeFacades;
        private static bool _excludeNonBrowsable;
        private static bool _warnOnMissingAssemblies;
    }
}
