// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Versioning;
using System;
using System.Text;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public class PackageDependencyChange
    {
        public BuildInfo BuildInfo { get; }
        public string PackageId { get; }
        public NuGetVersion Before { get; }
        public NuGetVersion After { get; }

        public PackageDependencyChange(
            BuildInfo buildInfo,
            string packageId,
            NuGetVersion before,
            NuGetVersion after)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            BuildInfo = buildInfo;
            PackageId = packageId;
            Before = before;
            After = after;
        }

        public override string ToString()
        {
            var message = new StringBuilder("'");
            message.Append(PackageId);

            if (Before != null)
            {
                message.Append(" ");
                message.Append(Before.ToNormalizedString());
            }
            message.Append("'");

            if (After != null)
            {
                message.Append(" has upgrade '");
                message.Append(After.ToNormalizedString());
                message.Append("'");
            }

            if (BuildInfo != null)
            {
                message.Append(" (");
                message.Append(BuildInfo.Name);
                message.Append(")");
            }
            return message.ToString();
        }
    }
}