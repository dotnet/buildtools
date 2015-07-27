// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Cci.Mappings
{
    public class MemberMapping : AttributesMapping<ITypeDefinitionMember>
    {
        public MemberMapping(TypeMapping containingType, MappingSettings settings)
            : base(settings)
        {
            this.ContainingType = containingType;
        }

        public TypeMapping ContainingType { get; private set; }
    }
}
