// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies.Submodule
{
    public class SubmoduleDependencyInfo : IDependencyInfo
    {
        public string Repository { get; set; }

        public string Ref { get; set; }

        public string Commit { get; set; }

        /// <summary>
        /// Unpin this dependency info so the current state will be fetched from the remote.
        /// </summary>
        public void Unpin()
        {
            Commit = null;
        }

        public override string ToString() => $"{SimpleName}:{Ref} ({Commit})";

        public string SimpleName => Repository.Split('/').Last();

        public string SimpleVersion => Commit?.Substring(0, Math.Min(7, Commit.Length)) ?? "latest";
    }
}
