﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public abstract class VersionsRepoUpdater
    {
        protected static IEnumerable<NupkgInfo> CreatePackageInfos(IEnumerable<string> packagePaths)
        {
            return packagePaths
                .Select(path => new NupkgInfo(path))
                // Ignore symbol packages.
                .Where(t => !t.SymbolPackage);
        }

        protected static Dictionary<string, string> CreatePackageInfoDictionary(IEnumerable<NupkgInfo> infos)
        {
            return infos.ToDictionary(i => i.Id, i => i.Version);
        }

        protected static string CreatePackageListFile(Dictionary<string, string> packages)
        {
            return string.Join(
                Environment.NewLine,
                packages
                    .OrderBy(t => t.Key)
                    .Select(t => $"{t.Key} {t.Value}"));
        }
    }
}
