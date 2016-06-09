using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Windows.Management.Deployment;
using Xunit.Abstractions;
using Xunit.Shared;

namespace Xunit.UwpClient
{
    public class Program
    {
        private const string runnerPath = @"app\XunitUwpRunner.exe";

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args[0] == "-?")
                {
                    PrintHeader();
                    PrintUsage();
                    return 2;
                }

                var commandLine = CommandLine.Parse(args);

                if (!commandLine.NoLogo)
                {
                    PrintHeader();
                }

                string installLocation = commandLine.InstallLocation == null ? Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()) : commandLine.InstallLocation;

                var test = new HostedAppxTest(args, commandLine.Project, runnerPath, installLocation);

                try
                {
                    test.Setup();
                    test.Run(commandLine.Debug);
                }
                finally
                {
                    test.Cleanup();
                }

                if (commandLine.Wait)
                {
                    Console.WriteLine();
                    Console.Write("Press any key to continue...");
                    Console.ReadKey();
                    Console.WriteLine();
                }

                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"error: {ex.Message}");
                return 3;
            }
            catch (BadImageFormatException ex)
            {
                Console.WriteLine(ex.Message);
                return 4;
            }
            finally
            {
                Console.ResetColor();
            }
        }

        static void PrintHeader()
        {
            Console.WriteLine($"xUnit.net UWP Runner ({IntPtr.Size * 8}-bit .NET {Environment.Version})");
        }

        static void PrintUsage()
        {
            var executableName = "xunit.console.uwp.exe";

            Console.WriteLine("Copyright (C) 2015 Outercurve Foundation.");
            Console.WriteLine();
            Console.WriteLine($"usage: {executableName} <assemblyFile> [configFile] [assemblyFile [configFile]...] [options] [reporter] [resultFormat filename [...]]");
            Console.WriteLine();
            Console.WriteLine("Note: Configuration files must end in .json (for JSON) or .config (for XML)");
            Console.WriteLine();
            Console.WriteLine("Console Runner options:");
            Console.WriteLine("  -installlocation                : path to install application");
            Console.WriteLine();
            Console.WriteLine("Valid options:");
            Console.WriteLine("  -nologo                : do not show the copyright message");
            Console.WriteLine("  -nocolor               : do not output results with colors");
            Console.WriteLine("  -noappdomain           : do not use app domains to run test code");
            Console.WriteLine("  -failskips             : convert skipped tests into failures");
            Console.WriteLine("  -parallel option       : set parallelization based on option");
            Console.WriteLine("                         :   none        - turn off all parallelization");
            Console.WriteLine("                         :   collections - only parallelize collections");
            Console.WriteLine("                         :   assemblies  - only parallelize assemblies");
            Console.WriteLine("                         :   all         - parallelize assemblies & collections");
            Console.WriteLine("  -maxthreads count      : maximum thread count for collection parallelization");
            Console.WriteLine("                         :   default   - run with default (1 thread per CPU thread)");
            Console.WriteLine("                         :   unlimited - run with unbounded thread count");
            Console.WriteLine("                         :   (number)  - limit task thread pool size to 'count'");
            Console.WriteLine("  -noshadow              : do not shadow copy assemblies");
            Console.WriteLine("  -wait                  : wait for input after completion");
            Console.WriteLine("  -diagnostics           : enable diagnostics messages for all test assemblies");
            Console.WriteLine("  -debug                 : launch the debugger to debug the tests");
            Console.WriteLine("  -serialize             : serialize all test cases (for diagnostic purposes only)");
            Console.WriteLine("  -trait \"name=value\"    : only run tests with matching name/value traits");
            Console.WriteLine("                         : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -notrait \"name=value\"  : do not run tests with matching name/value traits");
            Console.WriteLine("                         : if specified more than once, acts as an AND operation");
            Console.WriteLine("  -method \"name\"         : run a given test method (should be fully specified;");
            Console.WriteLine("                         : i.e., 'MyNamespace.MyClass.MyTestMethod')");
            Console.WriteLine("                         : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -class \"name\"          : run all methods in a given test class (should be fully");
            Console.WriteLine("                         : specified; i.e., 'MyNamespace.MyClass')");
            Console.WriteLine("                         : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -namespace \"name\"      : run all methods in a given namespace (i.e.,");
            Console.WriteLine("                         : 'MyNamespace.MySubNamespace')");
            Console.WriteLine("                         : if specified more than once, acts as an OR operation");
            Console.WriteLine();
        }

        static bool ValidateFileExists(object consoleLock, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || File.Exists(fileName))
                return true;

            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File not found: {fileName}");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            return false;
        }
    }
}
