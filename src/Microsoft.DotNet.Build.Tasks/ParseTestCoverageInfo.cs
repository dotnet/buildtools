// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;


namespace Microsoft.DotNet.Build.Tasks
{
    public class ParseTestCoverageInfo : BuildTask
    {
        // Path to the directory that contains *.coverage.xml coverage info.
        [Required]
        public ITaskItem[] CoverageReports { get; set; }

        // Name and path of the output XML
        [Required]
        public string OutputReport { get; set; }

        // One per xml parsed, key = test name, value list of modules visited
        private Dictionary<string, Dictionary<string, ModuleInfo>> testCoverageInfo = new Dictionary<string,Dictionary<string,ModuleInfo>>();

        public override bool Execute()
        {
            if (CoverageReports.Length == 0)
            {
                Log.LogError("CoverageReports cannot be empty");
                return false;
            }

            Log.LogMessage(MessageImportance.Normal, "{0} reports found.", CoverageReports.Length);

            foreach (var item in CoverageReports)
            {
                Log.LogMessage(MessageImportance.Normal, "Processing file: {0}", item.ItemSpec);
                
                ParseCoverageFile(item.ItemSpec);
            }

            SaveToFile();

            return true;
        }

        /// <summary>
        /// Parse all coverage .xml files
        /// </summary>
        private void ParseCoverageFile(string file)
        {
            XmlDocument coverageXml = new XmlDocument();
            coverageXml.Load(XmlReader.Create(file));

            string testAssemblyName = file.Replace(".coverage.xml", "");

            // get only the nodes for visited methods and do a bottom up parsing from that.
            XmlNodeList methods = coverageXml.SelectNodes("/CoverageSession/Modules/Module/Classes/Class/Methods/Method[not(contains(@visited, 'false'))]");

            Dictionary<string, ModuleInfo> visitedModules = new Dictionary<string, ModuleInfo>();

            foreach (XmlNode methodNode in methods)
            {
                // method < methods < class < classes < module
                XmlNode ModuleNode = methodNode.ParentNode.ParentNode.ParentNode.ParentNode;
                string moduleName = ModuleNode.SelectSingleNode("ModuleName").InnerText;

                ModuleInfo module = null;
                if (!visitedModules.TryGetValue(moduleName, out module))
                {
                    // Initialize module
                    module = new ModuleInfo(moduleName);

                    // Get files info
                    XmlNode filesNode = ModuleNode.SelectSingleNode("Files");
                    List<string> files = new List<string>(filesNode.ChildNodes.Count);
                    foreach (XmlNode moduleFile in filesNode.ChildNodes)
                    {
                        module.Files.Add(moduleFile.Attributes["fullPath"].Value);
                    }

                    visitedModules.Add(moduleName, module);
                }

                string methodSignature = methodNode["Name"].InnerText;
                // Signature format is "ReturnType Class::method(params)"
                string[] chunks = methodSignature.Split(new string[] { " ", "::", "(", ")" }, StringSplitOptions.RemoveEmptyEntries);
                string returnType = chunks[0];
                string className = chunks[1];
                string methodName = chunks[2];
                // not used
                // string methodParams = chunks[3];

                List<string> methodsCovered = null;
                if (!module.CoveredMethods.TryGetValue(className, out methodsCovered))
                {
                    methodsCovered = new List<string>();
                    module.CoveredMethods.Add(className, methodsCovered);
                }

                methodsCovered.Add(methodSignature);
            }
            testCoverageInfo.Add(testAssemblyName, visitedModules);
        }

        /// <summary>
        /// Save parsed information into a new file
        /// </summary>
        public void SaveToFile()
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;

            Log.LogMessage(MessageImportance.Normal, "Writing {0}", OutputReport);

            using (XmlWriter xWriter = XmlWriter.Create(File.OpenWrite(OutputReport), settings))
            {
                xWriter.WriteStartDocument();
                xWriter.WriteStartElement("Tests"); // <Tests>
                foreach (string testInfo in testCoverageInfo.Keys)
                {
                    xWriter.WriteStartElement("Test"); // <Test>
                    xWriter.WriteAttributeString("name", testInfo);
                    foreach (string moduleName in testCoverageInfo[testInfo].Keys)
                    {
                        xWriter.WriteStartElement("Module"); // <Module>
                        xWriter.WriteAttributeString("name", moduleName);
                        ModuleInfo moduleInfo = testCoverageInfo[testInfo][moduleName];
                        xWriter.WriteStartElement("Files");// <Files>
                        foreach (string file in moduleInfo.Files)
                        {
                            xWriter.WriteElementString("File", file); // <File />
                        }
                        xWriter.WriteEndElement(); // </Files>
                        xWriter.WriteStartElement("Classes");
                        foreach (string className in moduleInfo.CoveredMethods.Keys)
                        {
                            xWriter.WriteStartElement("Class"); // <Class>
                            xWriter.WriteAttributeString("name", className);
                            xWriter.WriteStartElement("Methods"); // <Mehtods>
                            foreach (string methodName in moduleInfo.CoveredMethods[className])
                            {
                                xWriter.WriteElementString("Method", methodName); // <Method />
                            }
                            xWriter.WriteEndElement(); // </Methods>
                            xWriter.WriteEndElement(); // </Class>
                        }
                        xWriter.WriteEndElement(); // </Classes>
                        xWriter.WriteEndElement(); // </Module>
                    }
                    xWriter.WriteEndElement(); // </Test>
                }
                xWriter.WriteEndElement(); // </Tests>
                xWriter.WriteEndDocument();
            }
        }
    }

    // shared class that represents coverage info in-memory
    // each test has a list of modules
    // all parsed data would look like List <pair<string, List<ModuleInfo>> tests; (string = test name)
    class ModuleInfo
    {
        public string FullName { get; set; }
        public List<string> Files { get; set; }

        // key = class
        // value = method (fully quialified signature)
        public Dictionary<string, List<string>> CoveredMethods { get; set; }

        public ModuleInfo(string moduleName)
        {
            FullName = moduleName;
            Files = new List<string>();
            CoveredMethods = new Dictionary<string, List<string>>();
        }
    }
}
