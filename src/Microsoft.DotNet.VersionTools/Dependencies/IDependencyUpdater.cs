// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    /// <summary>
    /// A tool that uses buildInfos to perform an update.
    /// </summary>
    public interface IDependencyUpdater
    {
        /// <summary>
        /// Updates based on the given build infos and returns build infos used during update.
        /// </summary>
        IEnumerable<BuildInfo> Update(IEnumerable<BuildInfo> buildInfos);
    }
}
