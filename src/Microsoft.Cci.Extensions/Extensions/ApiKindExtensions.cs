// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Cci.Extensions
{
    public static class ApiKindExtensions
    {
        public static ApiKind GetApiKind(this ITypeDefinition type)
        {
            return type.IsInterface
                ? ApiKind.Interface
                : type.IsDelegate
                    ? ApiKind.Delegate
                    : type.IsEnum
                        ? ApiKind.Enum
                        : type.IsStruct
                            ? ApiKind.Struct
                            : ApiKind.Class;
        }

        public static ApiKind GetApiKind(this ITypeDefinitionMember member)
        {
            if (member.ContainingTypeDefinition.IsDelegate)
                return ApiKind.DelegateMember;

            var field = member as IFieldDefinition;
            if (field != null)
            {
                if (member.ContainingTypeDefinition.IsEnum && field.IsSpecialName)
                    return ApiKind.EnumField;

                return ApiKind.Field;
            }

            if (member is IPropertyDefinition)
                return ApiKind.Property;

            if (member is IEventDefinition)
                return ApiKind.Event;

            var method = (IMethodDefinition)member;
            if (method.IsConstructor || method.IsStaticConstructor)
                return ApiKind.Constructor;

            var accessorType = method.GetAccessorType();
            switch (accessorType)
            {
                case AccessorType.PropertyGetter:
                case AccessorType.PropertySetter:
                    return ApiKind.PropertyAccessor;
                case AccessorType.EventAdder:
                case AccessorType.EventRemover:
                    return ApiKind.EventAccessor;
                default:
                    return ApiKind.Method;
            }
        }

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
