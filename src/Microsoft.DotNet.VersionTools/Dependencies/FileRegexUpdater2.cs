// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    /// <summary>
    /// New version of FileRegexUpdater that simplifies implementation by replacing "out" param with
    /// DependencyReplacement return. This API is shared with the new FileUpdater.
    /// </summary>
    public abstract class FileRegexUpdater2 : FileRegexUpdater
    {
        public abstract DependencyReplacement GetDesiredReplacement(
            IEnumerable<IDependencyInfo> dependencyInfos);

        protected override string TryGetDesiredValue(
            IEnumerable<IDependencyInfo> dependencyInfos,
            out IEnumerable<IDependencyInfo> usedDependencyInfos)
        {
            DependencyReplacement replacement = GetDesiredReplacement(dependencyInfos);
            usedDependencyInfos = (replacement?.UsedDependencyInfos).NullAsEmpty();
            return replacement?.Content;
        }
    }
}
