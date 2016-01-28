// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Cci.Extensions;
using System.IO;

namespace Microsoft.Cci.Filters
{
    public class DocIdExcludeListFilter : ICciFilter
    {
        private readonly HashSet<string> _docIds;

        public DocIdExcludeListFilter(IEnumerable<string> docIds)
        {
            _docIds = new HashSet<string>(docIds);
        }

        public DocIdExcludeListFilter(string whiteListFilePath)
        {
            _docIds = DocIdExtensions.ReadDocIds(whiteListFilePath);
        }

        public bool Include(INamespaceDefinition ns)
        {
            // Only include non-empty namespaces
            return ns.GetTypes().Any(Include);
        }

        public bool Include(ITypeDefinition type)
        {
            string typeId = type.DocId();

            // include so long as it isn't in the exclude list.
            return !_docIds.Contains(typeId);
        }

        public bool Include(ITypeDefinitionMember member)
        {
            string memberId = member.DocId();
            // include so long as it isn't in the exclude list.
            return !_docIds.Contains(memberId);
        }

        public bool Include(ICustomAttribute attribute)
        {
            string typeId = attribute.DocId();
            string removeUsages = "RemoveUsages:" + typeId;

            // special case: attribute usage can be removed without removing 
            //               the attribute itself
            if (_docIds.Contains(removeUsages))
                return false;

            // include so long as it isn't in the exclude list.
            return !_docIds.Contains(typeId);
        }
    }
}
