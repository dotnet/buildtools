// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Xml.Linq;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;

namespace Microsoft.DotNet.Build.Tasks
{ 
    public class ExtractResWResourcesFromAssemblies : BuildTask
    {
        [Required]
        public ITaskItem[] InputAssemblies { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public string InternalReswDirectory { get; set; }

        public override bool Execute()
        {
            try
            {
                Directory.CreateDirectory(OutputPath);
                CreateReswFiles();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: true);
            }

            return !Log.HasLoggedErrors;
        }

        public void CreateReswFiles()
        {
            foreach (ITaskItem assemblySpec in InputAssemblies)
            {
                string assemblyPath = assemblySpec.ItemSpec;
                string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

                if (assemblyName.Equals("System.Private.CoreLib"))
                {
                    continue; // we don't want to extract the resources from Private CoreLib since this resources are managed by the legacy ResourceManager which gets them from the embedded and not from PRI. 
                }

                if (!ShouldExtractResources($"FxResources.{Helper.NormalizeAssemblyName(assemblyName)}.SR.resw", assemblyPath))
                {
                    continue; // we skip framework assemblies that resources already exist and don't need to be extracted to avoid reading dll metadata.
                }

                try
                {
                    using (FileStream assemblyStream = File.OpenRead(assemblyPath))
                    using (PEReader peReader = new PEReader(assemblyStream))
                    {
                        if (!peReader.HasMetadata)
                        {
                            continue; // native assembly
                        }

                        MetadataReader metadataReader = peReader.GetMetadataReader();
                        foreach (ManifestResourceHandle resourceHandle in metadataReader.ManifestResources)
                        {
                            ManifestResource resource = metadataReader.GetManifestResource(resourceHandle);

                            if (!resource.Implementation.IsNil)
                            {
                                continue; // not embedded resource
                            }

                            string resourceName = metadataReader.GetString(resource.Name);

                            if (!resourceName.EndsWith(".resources"))
                            {
                                continue; // we only need to get the resources strings to produce the resw files.
                            }

                            string reswName = $"{Path.GetFileNameWithoutExtension(resourceName)}.resw";

                            if (!reswName.StartsWith("FxResources") && !ShouldExtractResources(reswName, assemblyPath)) // already checked for FxResources previously
                            {
                                continue; // resw output file already exists and is up to date, so we should skip this resource file.
                            }

                            string reswPath = Path.Combine(OutputPath, reswName);
                            using (Stream resourceStream = GetResourceStream(peReader, resource))
                            using (ResourceReader resourceReader = new ResourceReader(resourceStream))
                            using (ReswResourceWriter resourceWriter = new ReswResourceWriter(reswPath))
                            {
                                IDictionaryEnumerator enumerator = resourceReader.GetEnumerator();
                                while (enumerator.MoveNext())
                                {
                                    resourceWriter.AddResource(enumerator.Key.ToString(), enumerator.Value.ToString());
                                }
                            }
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    continue; // not a Portable Executable. 
                }
            }
        }

        // If the repo that we are building has some projects that contain an embedded resx file we will create the resw file for that project when building the src project in resources.targets
        // those resw files live in "InternalReswDirectory" so that is why in this case we don't need to check for a timestamp if the resw file already exists there we just skip those resources.
        // the reason why we skip it is because the incremental build will handle the timestamps, as we have a target to copy the resx files to resw files from EmbeddedResources inside the .csproj
        private bool ShouldExtractResources(string expectedReswFileName, string assemblyPath)
        {
            string internalReswPath = Path.Combine(InternalReswDirectory, expectedReswFileName);
            if (File.Exists(internalReswPath))
            {
                return false; // internal resw files are handled in build time by resources.targets, so we shouldn't care about timestamps since it uses incremental build
            }

            string externalReswPath = Path.Combine(OutputPath, expectedReswFileName);
            if (File.Exists(externalReswPath))
            {
                var reswFileInfo = new FileInfo(externalReswPath);
                var assemblyFileInfo = new FileInfo(assemblyPath);
                return reswFileInfo.LastWriteTimeUtc < assemblyFileInfo.LastWriteTimeUtc;
            }

            return true;
        }

        private unsafe Stream GetResourceStream(PEReader peReader, ManifestResource resource)
        {
            checked // arithmetic overflow here could cause AV
            {
                PEMemoryBlock memoryBlock = peReader.GetEntireImage();
                byte * peImageStart = memoryBlock.Pointer;
                byte * peImageEnd = peImageStart + memoryBlock.Length;

                // Locate resource's offset within the Portable Executable image.
                int resourcesDirectoryOffset;
                if (!peReader.PEHeaders.TryGetDirectoryOffset(peReader.PEHeaders.CorHeader.ResourcesDirectory, out resourcesDirectoryOffset))
                {
                    throw new InvalidDataException("Failed to extract the resources from assembly when getting the offset to resources in the PE file.");
                }

                byte * resourceStart = peImageStart + resourcesDirectoryOffset + resource.Offset;

                // We need to get the resource length out from the first int in the resourceStart pointer
                if (resourceStart >= peImageEnd - sizeof(int))
                {
                    throw new InvalidDataException("Failed to extract the resources from assembly because resource offset was out of bounds.");
                }

                int resourceLength = new BlobReader(resourceStart, sizeof(int)).ReadInt32();
                resourceStart += sizeof(int);
                if (resourceLength < 0 || resourceStart >= peImageEnd - resourceLength)
                {
                    throw new InvalidDataException($"Failed to extract the resources from assembly because resource offset or length was out of bounds.");
                }

                return new UnmanagedMemoryStream(resourceStart, resourceLength);
            }
        }
    }

    internal class ReswResourceWriter : IDisposable
    {
        private readonly XElement _root;

        private readonly XDocument _document;

        private readonly string _filePath;

        internal ReswResourceWriter(string filePath)
        {
            _filePath = filePath;
            _document = XDocument.Parse(headers);
            _root = _document.Element("root");
        }

        internal void AddResource(string key, string value)
        {
            XNamespace ns = "http://www.w3.org/XML/1998/namespace";
            var newElement = new XElement("data", 
                new XAttribute("name", key),
                new XAttribute(ns + "space", "preserve"),
                new XElement("value", value));

            _root.Add(newElement);
        }

        public void Dispose()
        {
            using (Stream fileStream = File.Create(_filePath))
            {
                _document.Save(fileStream);
            }
        }

        private string headers = 
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <root>
            <!--
            Microsoft ResX Schema 

            Version 2.0

            The primary goals of this format is to allow a simple XML format 
            that is mostly human readable. The generation and parsing of the 
            various data types are done through the TypeConverter classes 
            associated with the data types.

            Example:

            ... ado.net/XML headers & schema ...
            <resheader name=""resmimetype"">text/microsoft-resx</resheader>
            <resheader name=""version"">2.0</resheader>
            <resheader name=""reader"">System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>
            <resheader name=""writer"">System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>
            <data name=""Name1""><value>this is my long string</value><comment>this is a comment</comment></data>
            <data name=""Color1"" type=""System.Drawing.Color, System.Drawing"">Blue</data>
            <data name=""Bitmap1"" mimetype=""application/x-microsoft.net.object.binary.base64"">
                <value>[base64 mime encoded serialized .NET Framework object]</value>
            </data>
            <data name=""Icon1"" type=""System.Drawing.Icon, System.Drawing"" mimetype=""application/x-microsoft.net.object.bytearray.base64"">
                <value>[base64 mime encoded string representing a byte array form of the .NET Framework object]</value>
                <comment>This is a comment</comment>
            </data>
                        
            There are any number of ""resheader"" rows that contain simple 
            name/value pairs.

            Each data row contains a name, and value. The row also contains a 
            type or mimetype. Type corresponds to a .NET class that support 
            text/value conversion through the TypeConverter architecture. 
            Classes that don't support this are serialized and stored with the 
            mimetype set.

            The mimetype is used for serialized objects, and tells the 
            ResXResourceReader how to depersist the object. This is currently not 
            extensible. For a given mimetype the value must be set accordingly:

            Note - application/x-microsoft.net.object.binary.base64 is the format 
            that the ResXResourceWriter will generate, however the reader can 
            read any of the formats listed below.

            mimetype: application/x-microsoft.net.object.binary.base64
            value   : The object must be serialized with 
                    : System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
                    : and then encoded with base64 encoding.

            mimetype: application/x-microsoft.net.object.soap.base64
            value   : The object must be serialized with 
                    : System.Runtime.Serialization.Formatters.Soap.SoapFormatter
                    : and then encoded with base64 encoding.
            mimetype: application/x-microsoft.net.object.bytearray.base64
            value   : The object must be serialized into a byte array 
                    : using a System.ComponentModel.TypeConverter
                    : and then encoded with base64 encoding.
            -->
            <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
            <xsd:import namespace=""http://www.w3.org/XML/1998/namespace"" />
            <xsd:element name=""root"" msdata:IsDataSet=""true"">
                <xsd:complexType>
                <xsd:choice maxOccurs=""unbounded"">
                    <xsd:element name=""metadata"">
                    <xsd:complexType>
                        <xsd:sequence>
                        <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" />
                        </xsd:sequence>
                        <xsd:attribute name=""name"" use=""required"" type=""xsd:string"" />
                        <xsd:attribute name=""type"" type=""xsd:string"" />
                        <xsd:attribute name=""mimetype"" type=""xsd:string"" />
                        <xsd:attribute ref=""xml:space"" />
                    </xsd:complexType>
                    </xsd:element>
                    <xsd:element name=""assembly"">
                    <xsd:complexType>
                        <xsd:attribute name=""alias"" type=""xsd:string"" />
                        <xsd:attribute name=""name"" type=""xsd:string"" />
                    </xsd:complexType>
                    </xsd:element>
                    <xsd:element name=""data"">
                    <xsd:complexType>
                        <xsd:sequence>
                        <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                        <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
                        </xsd:sequence>
                        <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" msdata:Ordinal=""1"" />
                        <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
                        <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
                        <xsd:attribute ref=""xml:space"" />
                    </xsd:complexType>
                    </xsd:element>
                    <xsd:element name=""resheader"">
                    <xsd:complexType>
                        <xsd:sequence>
                        <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                        </xsd:sequence>
                        <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
                    </xsd:complexType>
                    </xsd:element>
                </xsd:choice>
                </xsd:complexType>
            </xsd:element>
            </xsd:schema>
            <resheader name=""resmimetype"">
            <value>text/microsoft-resx</value>
            </resheader>
            <resheader name=""version"">
            <value>2.0</value>
            </resheader>
            <resheader name=""reader"">
            <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
            </resheader>
            <resheader name=""writer"">
            <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
            </resheader>
            </root>";
    }
}
