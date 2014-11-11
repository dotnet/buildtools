// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;

namespace ResourceGenerator
{
    internal class ResGen
    {
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "/debug")
            {
                _debug = true;
                args = args.Skip(1).ToArray();
            }

            if (args.Length != 3 && args.Length != 4)
            {
                Console.WriteLine("Usage: ResourceGenrator [Optional Sourc Text File] [Optional Common Resources file] <Output source file> <AssemblyName>");
                return 1;
            }

            int result;
            ResGen resgen = null;
            try
            {
                resgen = args.Length == 3 ? new ResGen(args[0], null, args[1], args[2]) : new ResGen(args[0], args[1], args[2], args[3]);
                result = resgen.Run();
                resgen.Close();
            }
            catch (Exception e)
            {
                if (resgen != null)
                {
                    Console.WriteLine("Line Number = {0}", resgen._currentLine);
                    resgen.Close();
                }
                Console.WriteLine(e.Message);
                result = 2;
            }

            return result;
        }

        internal int Run()
        {
            WriteClassHeader();

            if (_firstSourceStream != null)
                RunOnResFile(true);
            if (_secondSourceStream != null)
                RunOnResFile(false);

            //            WriteValues();

            WriteClassEnd();

            return 0;
        }

        internal void RunOnResFile(bool isFirst)
        {
            StreamReader stream = (StreamReader)(isFirst ? _firstSourceStream : _secondSourceStream);
            MemoryStream streamActualJson = new MemoryStream();
            string s;
            // Our resjson files have comments in them, which isn't permitted by real json parsers. So, strip all lines that begin with comment characters.
            // This allows each line to have a comment, but doesn't allow comments after data. This is probably sufficient for our needs.
            while ((s = stream.ReadLine()) != null)
            {
                if (!s.Trim().StartsWith("//"))
                {
                    byte[] lineData = Encoding.Unicode.GetBytes(s + "\n");
                    streamActualJson.Write(lineData, 0, lineData.Length);
                }
            }
            streamActualJson.Seek(0, SeekOrigin.Begin);

            XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(streamActualJson, Encoding.Unicode, XmlDictionaryReaderQuotas.Max, null);
            XElement baseNode = XElement.Load(reader);
            foreach (XElement element in baseNode.Elements())
            {
                string elementDataString = (string)element;
                StoreValues(element.Name.LocalName, elementDataString);
            }
        }

        void StoreValues(string leftPart, string rightPart)
        {
            int value;
            if (_keys.TryGetValue(leftPart, out value))
            {
                return;
            }
            _keys[leftPart] = 0;
            if (_debug)
            {
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
                    _targetStream.WriteLine("        internal static string {0} {2}\n              get {2} return SR.GetResourceString(\"{0}\", @\"{1}\"); {3}\n        {3}", leftPart, sb.ToString(), "{", "}");
                }
                else
                {
                    _targetStream.WriteLine("        Friend Shared ReadOnly Property {0} As String\n            Get\n                Return SR.GetResourceString(\"{0}\", \"{1}\")\n            End Get\n        End Property", leftPart, sb.ToString());
                }
            }
            else
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
            // _list.Add(new ResData { Hash=HashString(leftPart), Key=leftPart, Value=rightPart });
        }
        /*
                private static int CompareResData(ResData x, ResData y)
                {
                    if (x.Hash == y.Hash) { return 0;  }
                    if (x.Hash > y.Hash) {return 1;  }
                    return -1;
                }

                void WriteValues()
                {
                    _list.Sort(CompareResData);
                    foreach (ResData d in _list)
                    {
                        _targetStream.WriteLine("            new ResData {0} Hash = {1}, Key = \"{2}\", Value = \"{3}\" {4}, ", "{", d.Hash, d.Key, d.Value, "}");
                    }
                }
        */
        ResGen(string firstSourceFile, string secondSourceFile, string targetFile, string assemblyName)
        {
            if (firstSourceFile != null)
                _firstSourceStream = File.OpenText(firstSourceFile);

            if (secondSourceFile != null)
                _secondSourceStream = File.OpenText(secondSourceFile);

            _targetStream = File.CreateText(targetFile);
            if (String.Equals(Path.GetExtension(targetFile), ".vb", StringComparison.OrdinalIgnoreCase))
            {
                _targetLanguage = TargetLanguage.VB;
            }

            _assemblyName = assemblyName;

            _currentLine = 0;
            _keys = new Dictionary<string, int>();
            //            _list = new List<ResData>();
        }

        void WriteClassHeader()
        {
            if (_targetLanguage == TargetLanguage.CSharp)
            {
                _targetStream.WriteLine("namespace System");
                _targetStream.WriteLine("{");


                _targetStream.WriteLine("    internal static partial class SR");
                _targetStream.WriteLine("    {");

                _targetStream.WriteLine("#pragma warning disable 0414");
                _targetStream.WriteLine("        private const string s_resourcesName = \"{0}\"; // assembly Name + .resources", _assemblyName + ".resources");
                _targetStream.WriteLine("#pragma warning restore 0414");
                _targetStream.WriteLine("");
            }
            else
            {
                _targetStream.WriteLine("Namespace System");

                _targetStream.WriteLine("    Friend Partial Class SR");
                _targetStream.WriteLine("    ");

                _targetStream.WriteLine("        Private Const s_resourcesName As String = \"{0}\" ' assembly Name + .resources", _assemblyName + ".resources");
                _targetStream.WriteLine("");
            }
        }

        void WriteClassEnd()
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

        void Close()
        {
            if (_firstSourceStream != null)
                _firstSourceStream.Close();
            if (_targetStream != null)
                _targetStream.Close();
            if (_secondSourceStream != null)
                _secondSourceStream.Close();
        }
        /*
                static int HashString(String str)
                {
                    string upper = str.ToUpperInvariant();
                    uint hash = 5381;
                    int c;

                    for (int i = 0; i < upper.Length; i++)
                    {
                        c = (int)upper[i];
                        hash = ((hash << 5) + hash) ^ (uint)c;
                    }

                    return (int) hash;
                }
        */
        enum TargetLanguage
        {
            CSharp, VB
        }
        private TargetLanguage _targetLanguage = TargetLanguage.CSharp;
        private StreamReader _firstSourceStream;
        private StreamReader _secondSourceStream;
        private StreamWriter _targetStream;
        private string _assemblyName;
        private int _currentLine;
        private Dictionary<string, int> _keys;

        private static bool _debug = false;
        //        private List<ResData> _list;

        /*
                class ResData
                {
                    public int      Hash { get; set; }
                    public string   Key { get; set; }
                    public string   Value { get; set; }
                }
         */
    }
}
