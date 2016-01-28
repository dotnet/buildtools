// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Traversers;

namespace Microsoft.Cci.Writers
{
    public class DocumentIdWriter : SimpleTypeMemberTraverser, ICciWriter
    {
        private readonly TextWriter _writer;

        public DocumentIdWriter(TextWriter writer, ICciFilter filter)
            : base(filter)
        {
            _writer = writer;
        }

        public void WriteAssemblies(IEnumerable<IAssembly> assemblies)
        {
            assemblies = assemblies.OrderBy(a => a.Name.Value);
            foreach (var assembly in assemblies)
                Visit(assembly);
        }

        public override void Visit(IAssembly assembly)
        {
            _writer.WriteLine(assembly.DocId());
            base.Visit(assembly);
        }

        public override void Visit(ITypeDefinition type)
        {
            _writer.WriteLine(type.DocId());
            base.Visit(type);
        }

        public override void Visit(ITypeDefinitionMember member)
        {
            _writer.WriteLine(member.DocId());
            base.Visit(member);
        }
    }
}
