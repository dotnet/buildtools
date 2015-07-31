// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    internal static class NativeMethods
    {
        internal const uint ERROR_INSUFFICIENT_BUFFER = 0x8007007A;
        internal const uint S_OK = 0x0;

        /// <summary>
        /// Get the runtime version for a given file
        /// </summary>
        /// <param name="szFullPath">The path of the file to be examined</param>
        /// <param name="szBuffer">The buffer allocated for the version information that is returned.</param>
        /// <param name="cchBuffer">The size, in wide characters, of szBuffer</param>
        /// <param name="dwLength">The size, in bytes, of the returned szBuffer.</param>
        /// <returns>HResult</returns>
        [DllImport("mscoree.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint GetFileVersion(string szFullPath, StringBuilder szBuffer, int cchBuffer, out uint dwLength);
    }
}
