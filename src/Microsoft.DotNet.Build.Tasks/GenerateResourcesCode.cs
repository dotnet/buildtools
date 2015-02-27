// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateResourcesCode : Task
    {
        [Required]
        public string ResxFilePath { get; set; }

        [Required]
        public string IntermediateFilePath { get; set; }

        [Required]
        public string OutputSourceFilePath { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        public bool DebugOnly { get; set; }

        public override bool Execute()
        {
            bool result = true;

            try
            {
                _resxReader = new ResXResourceReader(ResxFilePath);
                _intermediateFile = IntermediateFilePath + ".temp";
                _targetStream = File.CreateText(_intermediateFile);

                if (String.Equals(Path.GetExtension(OutputSourceFilePath), ".vb", StringComparison.OrdinalIgnoreCase))
                {
                    _targetLanguage = TargetLanguage.VB;
                }

                _keys = new Dictionary<string, int>();
                WriteClassHeader();
                RunOnResFile();
                WriteDebugCode();
                WriteClassEnd();
                Close();
                ProcessTargetFile();
            }
            catch (Exception e)
            {
                Close();

                Log.LogMessage(e.Message);

                if (e is System.UnauthorizedAccessException)
                {
                    Log.LogMessage("The generated {0} file needs to be updated but the file is read-only. Please checkout this file so it can be updated.", OutputSourceFilePath);
                }
                result = false; // fail the task
            }

            if (result)
            {
                // don't fail the task if this operation failed as we just updating intermediate file and the task already did the needed functionality of generating the code
                try { File.Move(_intermediateFile, IntermediateFilePath); } catch { }
            }

            return result;
        }
        private void Close()
        {
            if (_resxReader != null)
            {
                _resxReader.Close();
                _resxReader = null;
            }
            if (_targetStream != null)
            {
                _targetStream.Close();
                _targetStream = null;
            }
        }

        private void WriteClassHeader()
        {
            string commentPrefix = _targetLanguage == TargetLanguage.CSharp ? "// " : "' ";
            _targetStream.WriteLine(commentPrefix + "This is auto generated file. Please don\u2019t modify manually.");
            _targetStream.WriteLine(commentPrefix + "The file is generated as part of the build through the ResourceGenerator tool ");
            _targetStream.WriteLine(commentPrefix + "which takes the project resx resource file and generated this source code file.");
            _targetStream.WriteLine(commentPrefix + "By default the tool will use Resources\\Strings.resx but projects can customize");
            _targetStream.WriteLine(commentPrefix + "that by overriding the StringResourcesPath property group.");

            if (_targetLanguage == TargetLanguage.CSharp)
            {
                _targetStream.WriteLine("namespace System");
                _targetStream.WriteLine("{");


                _targetStream.WriteLine("    internal static partial class SR");
                _targetStream.WriteLine("    {");

                _targetStream.WriteLine("#pragma warning disable 0414");
                _targetStream.WriteLine("        private const string s_resourcesName = \"{0}\"; // assembly Name + .resources", AssemblyName + ".resources");
                _targetStream.WriteLine("#pragma warning restore 0414");
                _targetStream.WriteLine("");

                if (!DebugOnly)
                    _targetStream.WriteLine("#if !DEBUGRESOURCES");
            }
            else
            {
                _targetStream.WriteLine("Namespace System");

                _targetStream.WriteLine("    Friend Partial Class SR");
                _targetStream.WriteLine("    ");

                _targetStream.WriteLine("        Private Const s_resourcesName As String = \"{0}\" ' assembly Name + .resources", AssemblyName + ".resources");
                _targetStream.WriteLine("");
                if (!DebugOnly)
                    _targetStream.WriteLine("#If Not DEBUGRESOURCES Then");
            }
        }

        private void RunOnResFile()
        {
            IDictionaryEnumerator dict = _resxReader.GetEnumerator();
            while (dict.MoveNext())
            {
                StoreValues((string)dict.Key, (string)dict.Value);
            }
        }

        private void StoreValues(string leftPart, string rightPart)
        {
            int value;
            if (_keys.TryGetValue(leftPart, out value))
            {
                return;
            }
            _keys[leftPart] = 0;
            StringBuilder sb = new StringBuilder(rightPart.Length);
            for (var i = 0; i < rightPart.Length; i++)
            {
                // duplicate '"' for VB and C#
                if (rightPart[i] == '\"' && (_targetLanguage == TargetLanguage.VB || _targetLanguage == TargetLanguage.CSharp))
                {
                    sb.Append("\"");
                }
                sb.Append(rightPart[i]);
            }
            if (_targetLanguage == TargetLanguage.CSharp)
            {
                _debugCode.AppendFormat("        internal static string {0} {2}\n              get {2} return SR.GetResourceString(\"{0}\", @\"{1}\"); {3}\n        {3}\n", leftPart, sb.ToString(), "{", "}");
            }
            else
            {
                _debugCode.AppendFormat("        Friend Shared ReadOnly Property {0} As String\n            Get\n                Return SR.GetResourceString(\"{0}\", \"{1}\")\n            End Get\n        End Property\n", leftPart, sb.ToString());
            }

            if (!DebugOnly)
            {
                if (_targetLanguage == TargetLanguage.CSharp)
                {
                    _targetStream.WriteLine("        internal static string {0} {2}\n              get {2} return SR.GetResourceString(\"{0}\", {1}); {3}\n        {3}", leftPart, "null", "{", "}");
                }
                else
                {
                    _targetStream.WriteLine("        Friend Shared ReadOnly Property {0} As String\n           Get\n                 Return SR.GetResourceString(\"{0}\", {1})\n            End Get\n        End Property", leftPart, "Nothing");
                }
            }
        }

        private void WriteDebugCode()
        {
            if (_targetLanguage == TargetLanguage.CSharp)
            {
                if (!DebugOnly)
                    _targetStream.WriteLine("#else");
                _targetStream.WriteLine(_debugCode.ToString());
                if (!DebugOnly)
                    _targetStream.WriteLine("#endif");
            }
            else
            {
                if (!DebugOnly)
                    _targetStream.WriteLine("#Else");
                _targetStream.WriteLine(_debugCode.ToString());
                if (!DebugOnly)
                    _targetStream.WriteLine("#End If");
            }
        }

        private void WriteClassEnd()
        {
            if (_targetLanguage == TargetLanguage.CSharp)
            {
                _targetStream.WriteLine("    }");
                _targetStream.WriteLine("}");
            }
            else
            {
                _targetStream.WriteLine("    End Class");
                _targetStream.WriteLine("End Namespace");
            }
        }

        private void ProcessTargetFile()
        {
            string intermediateContent = File.ReadAllText(_intermediateFile);

            if (File.Exists(OutputSourceFilePath))
            {
                string srContent = File.ReadAllText(OutputSourceFilePath);

                if (intermediateContent == srContent)
                    return; // nothing need to get updated
            }

            File.WriteAllText(OutputSourceFilePath, intermediateContent);
        }

        private TargetLanguage _targetLanguage = TargetLanguage.CSharp;
        private ResXResourceReader _resxReader;
        private StreamWriter _targetStream;
        private string _intermediateFile;
        private StringBuilder _debugCode = new StringBuilder();
        private Dictionary<string, int> _keys;
        private enum TargetLanguage
        {
            CSharp, VB
        }
    }
}
