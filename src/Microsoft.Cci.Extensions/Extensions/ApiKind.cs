// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Cci.Extensions
{
    public enum ApiKind
    {
        Namespace = 0,
        Interface = 1,
        Delegate = 2,
        Enum,
        EnumField,
        Struct,
        Class,
        DelegateMember,
        Field,
        Property,
        Event,
        Constructor,
        PropertyAccessor,
        EventAccessor,
        Method
    }
}