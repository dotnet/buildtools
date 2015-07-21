// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Cci.Traversers;
using Microsoft.Cci.Extensions;
using System.IO;
using Microsoft.Cci.Filters;

namespace Microsoft.Cci.Writers
{
    public class TypeForwardWriter : SimpleTypeMemberTraverser, ICciWriter
    {
        private TextWriter _writer;
        public TypeForwardWriter(TextWriter writer, ICciFilter filter)
            : base(filter)
        {
            _writer = writer;
        }

        public void WriteAssemblies(IEnumerable<IAssembly> assemblies)
        {
            foreach (var assembly in assemblies)
                Visit(assembly);
        }

        public override void Visit(ITypeDefinition type)
        {
            if (IsForwardable(type))
            {
                _writer.WriteLine("[assembly:System.Runtime.CompilerServices.TypeForwardedTo(typeof({0}))]",
                    TypeHelper.GetTypeName(type, NameFormattingOptions.TypeParameters | NameFormattingOptions.EmptyTypeParameterList | NameFormattingOptions.UseTypeKeywords));
            }
            base.Visit(type);
        }

        public bool IsForwardable(ITypeDefinition type)
        {
            INestedTypeDefinition nestedType = type as INestedTypeDefinition;
            if (nestedType != null)
                return false;
            return true;
        }
    }
}
