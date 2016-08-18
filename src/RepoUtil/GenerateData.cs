// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace RepoUtil
{
    /// <summary>
    /// The repo tool will generate include files, props, etc ... that containt NuGet versions.  This struct contains
    /// information about where to generate and what packages should match.
    /// </summary>
    internal struct GenerateData
    {
        internal string RelativeFileName { get; }
        internal ImmutableArray<Regex> Packages { get; }

        internal GenerateData(string relativeFileName, ImmutableArray<Regex> packages)
        {
            RelativeFileName = relativeFileName;
            Packages = packages;
        }
    }
}
