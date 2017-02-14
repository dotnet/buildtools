// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Cci.Extensions
{
    public static class ApiKindExtensions
    {
        public static bool IsInfrastructure(this ApiKind kind)
        {
            switch (kind)
            {
                case ApiKind.EnumField:
                case ApiKind.DelegateMember:
                case ApiKind.PropertyAccessor:
                case ApiKind.EventAccessor:
                    return true;
                default:
                    return false;
            }
        }
    }
}
