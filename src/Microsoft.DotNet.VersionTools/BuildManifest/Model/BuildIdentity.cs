// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.DotNet.VersionTools.BuildManifest.Model
{
    public class BuildIdentity
    {
        public BuildIdentity(
            string name,
            string buildId,
            string branch = null,
            string commit = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Expected a non-empty string.", nameof(name));
            }
            Name = name;
            if (string.IsNullOrEmpty(buildId))
            {
                throw new ArgumentException("Expected a non-empty string.", nameof(buildId));
            }
            BuildId = buildId;
            Branch = branch;
            Commit = commit;
        }

        public string Name { get; }
        public string BuildId { get; }
        public string Branch { get; }
        public string Commit { get; }

        public override string ToString()
        {
            string s = $"{Name} '{BuildId}'";
            if (!string.IsNullOrEmpty(Branch))
            {
                s += $" on '{Branch}'";
            }
            if (!string.IsNullOrEmpty(Commit))
            {
                s += $" ({Commit})";
            }
            return s;
        }

        public IEnumerable<XAttribute> ToXml()
        {
            yield return new XAttribute(nameof(Name), Name);
            yield return new XAttribute(nameof(BuildId), BuildId);
            if (!string.IsNullOrEmpty(Branch))
            {
                yield return new XAttribute(nameof(Branch), Branch);
            }
            if (!string.IsNullOrEmpty(Commit))
            {
                yield return new XAttribute(nameof(Commit), Commit);
            }
        }

        public static BuildIdentity Parse(XElement xml)
        {
            return new BuildIdentity(
                xml.GetRequiredAttribute(nameof(Name)),
                xml.GetRequiredAttribute(nameof(BuildId)),
                xml.Attribute(nameof(Branch))?.Value,
                xml.Attribute(nameof(Commit))?.Value);
        }
    }
}
