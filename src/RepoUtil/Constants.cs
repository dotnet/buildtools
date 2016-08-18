// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace RepoUtil
{
    internal static class Constants
    {
        /// <summary>
        /// NuGet package names are not case sensitive.
        /// </summary>
        internal static readonly StringComparer NugetPackageNameComparer = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// NuGet package versions case sensitivity is not documented anywhere that could be found.  Assuming
        /// case insensitive for now.
        /// </summary>
        internal static readonly StringComparer NugetPackageVersionComparer = StringComparer.OrdinalIgnoreCase;
    }
}
