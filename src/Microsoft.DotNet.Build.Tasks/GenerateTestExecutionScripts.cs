using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateTestExecutionScripts : Task
    {
        [Required]
        public string[] TestCommands { get; set; }

        [Required]
        public ITaskItem[] TestDependencies { get; set; }

        [Required]
        public string RunnerScriptTemplate { get; set; }

        [Required]
        public string ScriptOutputPath { get; set; }

        public override bool Execute()
        {
            if (TestCommands.Length == 0)
            {
                throw new InvalidOperationException("Please provide at least one test command To execute via the TestCommands property.");
            }

            if (!File.Exists(RunnerScriptTemplate))
            {
                throw new FileNotFoundException($"Runner script template {RunnerScriptTemplate} was not found.");
            }

            string executionScriptTemplate = File.ReadAllText(RunnerScriptTemplate);
            Directory.CreateDirectory(Path.GetDirectoryName(ScriptOutputPath));

            Log.LogMessage($"Test Command lines = {string.Join(Environment.NewLine, TestCommands)}");
            string extension = Path.GetExtension(Path.GetFileName(ScriptOutputPath)).ToLowerInvariant();
            switch (extension)
            {
                case ".sh":
                    WriteShExecutionScript(executionScriptTemplate, ScriptOutputPath);
                    break;
                case ".cmd":
                case ".bat":
                    WriteCmdExecutionScript(executionScriptTemplate, ScriptOutputPath);
                    break;
                default:
                    throw new System.NotSupportedException($"Generating runner scripts with extension '{extension}' is not yet supported");
            }
            return true;
        }

        private void WriteShExecutionScript(string shExecutionTemplate, string outputPath)
        {
            // Build up the copy commands... 
            StringBuilder copyCommands = new StringBuilder();
            foreach (ITaskItem dependency in TestDependencies)
            {
                string relativePath = dependency.GetMetadata("PackageRelativePath");
                bool? useAbsolutePath = dependency.GetMetadata("UseAbsolutePath")?.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (useAbsolutePath == true)
                {
                    string filePath = dependency.GetMetadata("SourcePath");
                    string fileName = Path.GetFileName(filePath);
                    copyCommands.Append($"copy_and_check {filePath} $EXECUTION_DIR/{fileName}\n");
                }

                // Generally anything without the relative path is just the test DLL and its directly referenced dependencies.  
                // Every test project comes with 4 of them, so not producing a warning here.
                else if (!string.IsNullOrEmpty(relativePath))
                {
                    string normalizedDependency = relativePath.Replace('\\', '/');
                    if (normalizedDependency.StartsWith("/"))
                    {
                        normalizedDependency = normalizedDependency.Substring(1);
                    }
                    string fileName = Path.GetFileName(normalizedDependency);
                    copyCommands.Append($"copy_and_check $PACKAGE_DIR/{normalizedDependency} $EXECUTION_DIR/{fileName}\n");
                }
            }
            shExecutionTemplate = shExecutionTemplate.Replace("[[CopyFilesCommands]]", copyCommands.ToString());

            StringBuilder testRunEchoes = new StringBuilder();
            StringBuilder testRunCommands = new StringBuilder();
            foreach (string runCommand in TestCommands)
            {
                testRunCommands.Append($"{runCommand}\n");
                testRunEchoes.Append($"echo {runCommand}\n");
            }
            shExecutionTemplate = shExecutionTemplate.Replace("[[TestRunCommands]]", testRunCommands.ToString());
            shExecutionTemplate = shExecutionTemplate.Replace("[[TestRunCommandsEcho]]", testRunEchoes.ToString());
            // Just in case any Windows EOLs have made it in by here, clean any up.
            shExecutionTemplate = shExecutionTemplate.Replace("\r\n", "\n");

            using (StreamWriter sw = new StreamWriter(new FileStream(outputPath, FileMode.Create)))
            {
                sw.NewLine = "\n";
                sw.Write(shExecutionTemplate);
                sw.WriteLine();
            }
            Log.LogMessage($"Wrote .sh test execution script to {outputPath}");
        }

        private void WriteCmdExecutionScript(string cmdExecutionTemplate, string outputPath)
        {
            // Build up the copy commands... 
            StringBuilder copyCommands = new StringBuilder();
            foreach (ITaskItem dependency in TestDependencies)
            {
                string relativePath = dependency.GetMetadata("PackageRelativePath");
                bool? useAbsolutePath = dependency.GetMetadata("UseAbsolutePath")?.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (useAbsolutePath == true)
                {
                    string fullPath = dependency.GetMetadata("SourcePath");
                    fullPath = fullPath.Replace('/', '\\');
                    copyCommands.AppendLine($"call :copyandcheck \"{fullPath}\" \"%EXECUTION_DIR%/{Path.GetFileName(fullPath)}\" || exit /b -1");
                }
                // Generally anything without the relative path is just the test DLL and its directly referenced dependencies.  
                // Every test project comes with 4 of them, so not producing a warning here.
                else if (!string.IsNullOrEmpty(relativePath))
                {
                    copyCommands.AppendLine($"call :copyandcheck \"%PACKAGE_DIR%\\{relativePath}\" \"%EXECUTION_DIR%\\{Path.GetFileName(relativePath)}\" ||  exit /b -1");
                }
            }
            cmdExecutionTemplate = cmdExecutionTemplate.Replace("[[CopyFilesCommands]]", copyCommands.ToString());

            // Same thing with execution commands
            StringBuilder testRunEchoes = new StringBuilder();
            StringBuilder testRunCommands = new StringBuilder();
            foreach (string runCommand in TestCommands)
            {
                testRunCommands.AppendLine($"{runCommand}");
                testRunEchoes.AppendLine($"echo {runCommand}");
            }

            cmdExecutionTemplate = cmdExecutionTemplate.Replace("[[TestRunCommands]]", testRunCommands.ToString());
            cmdExecutionTemplate = cmdExecutionTemplate.Replace("[[TestRunCommandsEcho]]", testRunEchoes.ToString());

            using (StreamWriter sw = new StreamWriter(new FileStream(outputPath, FileMode.Create)))
            {
                sw.Write(cmdExecutionTemplate);
                sw.WriteLine();
            }
            Log.LogMessage($"Wrote Windows-compatible test execution script to {outputPath}");
        }
    }
}