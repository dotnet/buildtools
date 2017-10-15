// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public interface IDependencyInfo
    {
        /// <summary>
        /// A simple, short name for this dependency info. Used in the suggested commit message.
        /// </summary>
        string SimpleName { get; }

        /// <summary>
        /// A simple, short name for the overall version of this dependency info. Used in the
        /// suggested commit message.
        /// </summary>
        string SimpleVersion { get; }
    }
}
