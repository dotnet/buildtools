// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Build.Tasks
{
    internal delegate bool DirectoryExists(string path);
    internal delegate bool FileExists(string path);
    internal delegate string TryGetRuntimeVersion(string path);
}
