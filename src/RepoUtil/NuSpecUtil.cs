// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RepoUtil
{
    internal static class NuSpecUtil
    {
        internal static IEnumerable<FileName> GetNuSpecFiles(string sourcesPath)
        {
            return Directory
                .EnumerateFiles(sourcesPath, "*.nuspec", SearchOption.AllDirectories)
                .Select(x => FileName.FromFullPath(sourcesPath, x));
        }

        internal static string GetId(string nuspecFilePath)
        {
            var doc = XDocument.Load(nuspecFilePath);
            var ns = XNamespace.Get("http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd");
            var id = doc
                .Element(ns.GetName("package"))
                .Element(ns.GetName("metadata"))
                .Element(ns.GetName("id"))
                .Value;
            return id;
        }
    }
}
