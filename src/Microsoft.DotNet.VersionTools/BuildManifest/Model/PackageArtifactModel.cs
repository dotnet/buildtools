// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.DotNet.VersionTools.BuildManifest.Model
{
    public class PackageArtifactModel
    {
        private static readonly string[] AttributeOrder =
        {
            nameof(Id),
            nameof(Version)
        };

        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public string Id
        {
            get { return Attributes[nameof(Id)]; }
            set { Attributes[nameof(Id)] = value; }
        }

        public string Version
        {
            get { return Attributes[nameof(Version)]; }
            set { Attributes[nameof(Version)] = value; }
        }

        public override string ToString() => $"Package {Id} {Version}";

        public XElement ToXml() => new XElement(
            "Package",
            Attributes.CreateXmlAttributes(AttributeOrder));

        public static PackageArtifactModel Parse(XElement xml) => new PackageArtifactModel
        {
            Id = xml.GetRequiredAttribute(nameof(Id)),
            Version = xml.GetRequiredAttribute(nameof(Version)),
            Attributes = xml.CreateAttributeDictionary()
        };
    }
}
