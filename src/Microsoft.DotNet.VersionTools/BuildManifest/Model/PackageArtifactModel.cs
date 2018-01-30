// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
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

        public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public string Id
        {
            get { return Attributes.GetOrDefault(nameof(Id)); }
            set { Attributes[nameof(Id)] = value; }
        }

        public string Version
        {
            get { return Attributes.GetOrDefault(nameof(Version)); }
            set { Attributes[nameof(Version)] = value; }
        }

        public override string ToString() => $"Package {Id} {Version}";

        public XElement ToXml() => new XElement(
            "Package",
            Attributes
                .ThrowIfMissingAttributes(AttributeOrder)
                .CreateXmlAttributes(AttributeOrder));

        public static PackageArtifactModel Parse(XElement xml) => new PackageArtifactModel
        {
            Attributes = xml
                .CreateAttributeDictionary()
                .ThrowIfMissingAttributes(AttributeOrder)
        };
    }
}
