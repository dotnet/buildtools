// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Mappings;

namespace Microsoft.Cci.Filters
{
    public class TypesOnlyMappingDifferenceFilter : MappingDifferenceFilter
    {
        public TypesOnlyMappingDifferenceFilter(Func<DifferenceType, bool> include, ICciFilter filter)
            : base(include, filter)
        {
        }

        public override bool Include(MemberMapping member)
        {
            return false;
        }
    }
}
