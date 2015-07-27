// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Cci.Filters
{
    public interface ICciFilter
    {
        bool Include(INamespaceDefinition ns);
        bool Include(ITypeDefinition type);
        bool Include(ITypeDefinitionMember member);
        bool Include(ICustomAttribute attribute); // Used to filter the application of attributes as opposed to attribute types
    }
}
