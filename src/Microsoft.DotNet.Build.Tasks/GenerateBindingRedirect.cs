using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateBindingRedirect : Task
    {
        [Required]
        public ITaskItem[] AssemblyIdentities { get; set; }

        [Required]
        public ITaskItem[] Executables { get; set; }

        [Required]
        public string OutputPath { get; set; }

        private static XNamespace ns { get; set; }

        public override bool Execute()
        {
            ns = "urn:schemas-microsoft-com:asm.v1";
            XElement bindingRedirectAssemblies = new XElement(ns + "assemblyBinding");
            foreach (ITaskItem assembly in AssemblyIdentities)
            {
                string publicKeyToken = assembly.GetMetadata("PublicKeyToken");
                string assemblyVersion = assembly.GetMetadata("Version");
                string assemblyName = assembly.GetMetadata("Name");
                if (string.IsNullOrEmpty(publicKeyToken))
                {
                    Log.LogWarning($"Empty publicKeyToken for {assemblyName} {assemblyVersion}");
                }
                string culture = string.IsNullOrEmpty(assembly.GetMetadata("Culture"))
                    ? "neutral"
                    : assembly.GetMetadata("Culture");
                XElement assemblyIdentity = new XElement(ns + "assemblyIdentity",
                    new XAttribute("name", assemblyName),
                    new XAttribute("publicKeyToken", publicKeyToken),
                    new XAttribute("culture", culture));
                XElement bindingRedirect = new XElement(ns + "bindingRedirect",
                    new XAttribute("oldVersion", $"0.0.0.0-{assemblyVersion}"),
                    new XAttribute("newVersion", assemblyVersion));
                XElement dependentAssembly = new XElement(ns + "dependentAssembly",
                    assemblyIdentity,
                    bindingRedirect);
                bindingRedirectAssemblies.Add(dependentAssembly);
            }
            XDocument doc = new XDocument(new XElement("configuration", new XElement("runtime", bindingRedirectAssemblies)));
            foreach (ITaskItem executable in Executables)
            {
                string executableName = Path.GetFileName(executable.ItemSpec);
                using (FileStream fs = new FileStream(Path.Combine(OutputPath, executableName + ".config"), FileMode.Create))
                {
                    doc.Save(fs);
                }
            }
            
            return !Log.HasLoggedErrors;
        }
    }
}
