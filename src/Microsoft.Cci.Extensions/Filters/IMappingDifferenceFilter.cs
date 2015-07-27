// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Cci.Mappings;
using Microsoft.Cci.Differs;

namespace Microsoft.Cci.Filters
{
    public interface IMappingDifferenceFilter
    {
        bool Include(AssemblyMapping assembly);
        bool Include(NamespaceMapping ns);
        bool Include(TypeMapping type);
        bool Include(MemberMapping member);
        bool Include(DifferenceType difference);
    }
}
