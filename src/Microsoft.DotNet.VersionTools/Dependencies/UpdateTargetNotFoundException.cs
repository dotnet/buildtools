// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    /// <summary>
    /// The exception that is thrown when an invalid dependency upgrade is attempted.
    /// </summary>
    public class UpdateTargetNotFoundException : Exception
    {
        public UpdateTargetNotFoundException()
        {
        }

        public UpdateTargetNotFoundException(string message) : base(message)
        {
        }

        public UpdateTargetNotFoundException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
