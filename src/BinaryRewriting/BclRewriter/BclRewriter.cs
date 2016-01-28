// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using Microsoft.Tools.Transformer.CodeModel;
using SimpleTimer;
using System;
using System.Diagnostics;
using System.IO;
using Thinner;
using TrimBin;

namespace BclRewriter
{
    public class UsageException : Exception
    {
        public UsageException()
            : base()
        { }
        public UsageException(String message)
            : base(message)
        { }
    }

    internal class Program
    {
        #region Commandline options
#if ATTRIBUTE_EXEMPTIONS
    [Argument(ArgumentType.MultipleUnique, HelpText = "Atribute to exempt from whatever polarity keepAttributes is", ShortName = "a", LongName = "attribute")]
    private static string[] _attrs = null;
#endif

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "Code model xml file with types and members to include.",
                  DefaultValue = "", ShortName = "inc", LongName = "include")]
        private static string s_includeListFile = "";

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "Platform to filter model xml.",
                  DefaultValue = "", ShortName = "p", LongName = "platform")]
        private static string s_platform = "";

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "Architecture to filter model xml.",
                  DefaultValue = "", ShortName = "a", LongName = "architecture")]
        private static string s_architecture = "";

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "Flavor to filter model xml.",
                  DefaultValue = "", ShortName = "f", LongName = "flavor")]
        private static string s_flavor = "";

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "C# defines to filter model xml.",
                  DefaultValue = "", ShortName = "d", LongName = "define")]
        private static string s_defines = "";

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "Remove Serializable attribute",
                  DefaultValue = true, ShortName = "r", LongName = "removeSerializable")]
        private static bool s_removeSerializable = true;

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "Output path (including filename) for the trimmed assembly.",
                  DefaultValue = null, ShortName = "o", LongName = "out")]
        private static string s_output = null;

        [Argument(ArgumentType.Required,
                  HelpText = "Assembly to process.",
                  ShortName = "i", LongName = "in")]
        private static string s_assemblyName = null;

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "Keep temporary files (closure xml files).",
                  DefaultValue = false, ShortName = "ktf", LongName = "keepTempFiles")]
        private static bool s_keepTempFiles = false;

        [Argument(ArgumentType.AtMostOnce,
                  HelpText = "Treat ApiFxInternal model elements as ApiRoot (required to produce a reference assembly for test builds).",
                  DefaultValue = false, ShortName = "fxAsPub", LongName = "fxInternalAsPublic")]
        private static bool s_treatFxInternalAsPublic = false;

        [Argument(ArgumentType.Multiple, HelpText = "Assemblies referenced by the current assembly.", DefaultValue = new string[0], ShortName = "ref", LongName = "referenced")]
        private static string[] s_referencedAssemblies = null;

        [Argument(ArgumentType.MultipleUnique, HelpText = "Paths to probe to resolve assembly dependencies.", DefaultValue = new string[0], ShortName = "adp", LongName = "assemblyDependencyPath")]
        private static string[] s_assemblyDependencyPaths = null;

        #endregion Commandline options

        private static int Main(string[] args)
        {
            try
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();
                RunBclRewriter(args);
                watch.Stop();
                Console.WriteLine("Total elapsed time: {0}", watch.Elapsed);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("BclRewriter : error BR0000 : {0}", e.Message);
                return 1;
            }
        }

        private const string TempExtension = ".preRewrite";

        public static void RunBclRewriter(string[] args)
        {
            #region Parse the command-line arguments.
            if (!Parser.ParseArgumentsWithUsage(args, typeof(Program)))
                throw new UsageException();
            #endregion

            #region Figure out paths
            s_assemblyName = Path.GetFullPath(s_assemblyName); // this has to be specified

            string outputBaseName = null;
            if (!String.IsNullOrEmpty(s_output))
            {
                s_output = Path.GetFullPath(s_output);
                outputBaseName = Path.GetFileNameWithoutExtension(s_output);
            }
            else
            {
                s_output = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileNameWithoutExtension(s_assemblyName) + ".small" + Path.GetExtension(s_assemblyName));
                outputBaseName = s_assemblyName;
            }

            string pdbSourceFile = Path.ChangeExtension(s_assemblyName, "pdb");
            string outputPdb = Path.ChangeExtension(s_output, "pdb");
            string outputFolder = Path.GetDirectoryName(s_output);

            // if the user wants to do an in-place rewrite, we copy the file to a temp file
            if (s_output == s_assemblyName)
            {
                String tempPath = s_assemblyName + TempExtension;
                String tempPdbPath = pdbSourceFile + TempExtension;

                File.Copy(s_assemblyName, tempPath, true);
                s_assemblyName = tempPath;

                if (File.Exists(pdbSourceFile))
                {
                    File.Copy(pdbSourceFile, tempPdbPath, true);
                    pdbSourceFile = tempPdbPath;
                }
            }

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            #endregion

            #region Load input files
            HostEnvironment host = new HostEnvironment(new NameTable(), s_assemblyDependencyPaths, s_referencedAssemblies);

            IAssembly/*?*/ assembly = host.LoadUnitFrom(s_assemblyName) as IAssembly;
            // TODO: Handle multimodule assemblies
            if (assembly == null || assembly == Dummy.Assembly)
            {
                throw new UsageException(args[0] + " is not a PE file containing a CLR assembly, or an error occurred when loading it.");
            }

            if (!File.Exists(s_includeListFile))
            {
                throw new UsageException(String.Format("ERROR: Can't find code model file '{0}'", s_includeListFile));
            }

            ThinModel model = new ThinModel(new ThinnerOptions(host, new AssemblyIdentity[] { assembly.AssemblyIdentity }));
            model.LoadModel(s_includeListFile, new ModelReaderOptions(s_platform, s_architecture, s_flavor, s_treatFxInternalAsPublic, s_defines));
            #endregion

            #region Calculate api closure.
            ConsoleTimer.StartTimer("Calculating api closure");
            model.LoadMetadataFrom(assembly);

            ThinModel apiClosure = model.CalculateApiClosure();
            if (s_keepTempFiles)
                apiClosure.SaveModel(Path.ChangeExtension(s_output, ".apiClosure.xml"));
            ConsoleTimer.EndTimer("Calculating api closure");
            #endregion

            #region Calculate impl closure.
            ConsoleTimer.StartTimer("Calculating implementation closure");
            apiClosure.LoadMetadataFrom(assembly);

            ThinModel implClosure = apiClosure.CalculateImplementationClosure(true, FieldOptions.KeepAll);

            if (s_keepTempFiles)
                implClosure.SaveModel(Path.ChangeExtension(s_output, ".implClosure.xml"));
            ConsoleTimer.EndTimer("Calculating implementation closure");
            #endregion

            #region Trim.
            ConsoleTimer.StartTimer("Trimming assembly");
            IncludeSet includeSet = new IncludeSet();
            includeSet.LoadFrom(implClosure);

            var copier = new MetadataDeepCopier(host);
            Assembly copiedAssembly = copier.Copy(assembly);

            Trimmer trimmer = new Trimmer(includeSet, true, false, true, host, s_removeSerializable);
            trimmer.RewriteChildren(copiedAssembly);
            Assembly mutableAssembly = copiedAssembly;
            assembly = mutableAssembly;

            ConsoleTimer.EndTimer("Trimming assembly");
            #endregion

            #region Update assembly name.
            ConsoleTimer.StartTimer("Updating assembly name");

            // If the output assembly name is different, update the internal assembly name to match.
            AssemblyIdentity originalAssemblyIdentity = mutableAssembly.AssemblyIdentity;
            if (!outputBaseName.Equals(originalAssemblyIdentity.Name.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                mutableAssembly.Name = host.NameTable.GetNameFor(outputBaseName);
                mutableAssembly.ModuleName = mutableAssembly.Name;
            }

            // If we changed the assembly identity, update references to it.
            if (!mutableAssembly.AssemblyIdentity.Equals(originalAssemblyIdentity))
            {
                trimmer.UpdateAssemblyReferences(originalAssemblyIdentity, mutableAssembly.AssemblyIdentity);
            }

            ConsoleTimer.EndTimer("Updating assembly name");
            #endregion

            #region Write out the assembly
            ConsoleTimer.StartTimer("Writing assembly");
            PdbReader pdbReader = null;
            PdbWriter pdbWriter = null;
            if (File.Exists(pdbSourceFile))
            {
                Stream pdbStream = File.OpenRead(pdbSourceFile);
                pdbReader = new PdbReader(pdbStream, host);
                pdbWriter = new PdbWriter(outputPdb, pdbReader);
                Console.WriteLine("Writing pdb: {0}", outputPdb);
            }

            Console.WriteLine("Writing assembly: {0}", s_output);
            FileStream file = File.Create(s_output);

            try
            {
                PeWriter.WritePeToStream(assembly, host, file, pdbReader, pdbReader, pdbWriter);
            }
            finally
            {
                if (file != null)
                {
                    file.Dispose();
                }

                if (pdbWriter != null)
                {
                    pdbWriter.Dispose();
                }
            }

            ConsoleTimer.EndTimer("Writing assembly");
            #endregion
        }
    }
}
