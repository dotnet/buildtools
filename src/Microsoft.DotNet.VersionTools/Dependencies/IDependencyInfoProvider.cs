// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public interface IDependencyInfoProvider : IDependencyUpdater
    {
        /// <summary>
        /// Create a dependency info that this updater depends on. An updater implements this when
        /// no "outside" information is required to determine the current state of the repo's
        /// dependency.
        /// 
        /// For example, for a submodule, the updater knows the submodule's current state using Git
        /// commands to query it. The created dependency build info can modified by the caller to
        /// change the repo's state, for example, to update a submodule to the latest remote commit.
        /// 
        /// (This is not implemented when the current repo state depends on "outside" info, such as
        /// a package list on dotnet/versions.)
        /// </summary>
        IEnumerable<IDependencyInfo> CreateDependencyInfos();
    }
}
