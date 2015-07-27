// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Cci.Extensions;

namespace Microsoft.Cci.Traversers
{
    public class DocIdToTypeMappingTraverser : SimpleTypeMemberTraverser
    {
        private readonly Dictionary<string, ITypeDefinition> _typesIdMap;

        public DocIdToTypeMappingTraverser() : base(null)
        {
            _typesIdMap = new Dictionary<string, ITypeDefinition>();
        }

        public ITypeDefinition GetTypeFromDocId(string typeDocId)
        {
            ITypeDefinition type = null;
            _typesIdMap.TryGetValue(typeDocId, out type);
            return type;
        }

        public override void Visit(ITypeDefinition type)
        {
            _typesIdMap[type.UniqueId()] = type;
            base.Visit(type);
        }
    }
}
