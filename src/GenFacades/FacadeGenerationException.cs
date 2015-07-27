// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace GenFacades
{
    internal sealed class FacadeGenerationException : Exception
    {
        public FacadeGenerationException(string message) : base(message)
        {
        }
    }
}
