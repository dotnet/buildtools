// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Internal type used for JSON serialization of signing certificate data.
    /// </summary>
    internal sealed class SignTypeItem
    {
        /// <summary>
        /// Gets or sets the name of the Authenticode signature to apply.
        /// </summary>
        public string Authenticode { get; set; }

        /// <summary>
        /// Gets or sets the name of the strong-name signature to apply.
        /// </summary>
        public string StrongName { get; set; }
    }
}
