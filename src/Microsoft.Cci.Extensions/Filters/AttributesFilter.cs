// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Cci.Filters
{
    public sealed class AttributesFilter : ICciFilter
    {
        private readonly bool _includeAttributes;

        public AttributesFilter(bool includeAttributes)
        {
            _includeAttributes = includeAttributes;
        }

        public bool Include(INamespaceDefinition ns)
        {
            return true;
        }

        public bool Include(ITypeDefinition type)
        {
            return true;
        }

        public bool Include(ITypeDefinitionMember member)
        {
            return true;
        }

        public bool Include(ICustomAttribute attribute)
        {
            return _includeAttributes;
        }
    }
}