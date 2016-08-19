// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace RepoUtil
{
    internal struct FileName : IEquatable<FileName>
    {
        internal string Name { get; }
        internal string FullPath { get; }
        internal string RelativePath { get; }

        internal FileName(string rootPath, string relativePath)
        {
            Name = Path.GetFileName(relativePath);
            FullPath = Path.Combine(rootPath, relativePath);
            RelativePath = relativePath;
        }

        internal static FileName FromFullPath(string rootPath, string fullPath)
        {
            fullPath = fullPath.Substring(rootPath.Length + 1);
            return new FileName(rootPath, fullPath);
        }

        public static bool operator ==(FileName left, FileName right) => left.FullPath == right.FullPath;
        public static bool operator !=(FileName left, FileName right) => !(left == right);
        public bool Equals(FileName other) => this == other;
        public override int GetHashCode() => FullPath.GetHashCode();
        public override string ToString() => RelativePath;
        public override bool Equals(object obj) => obj is FileName && Equals((FileName)obj);
    }
}
