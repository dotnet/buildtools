// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter
    {
        private void WriteEventDefinition(IEventDefinition evnt)
        {
            // Adder and Remover modifiers should be same.
            IMethodDefinition accessor = evnt.Accessors.First().ResolvedMethod;

            if (!evnt.ContainingTypeDefinition.IsInterface)
            {
                WriteAttributes(evnt.Attributes);
                if (!accessor.IsExplicitInterfaceMethod())
                    WriteVisibility(evnt.Visibility);
                WriteMethodModifiers(accessor);
            }

            if (evnt.GetHiddenBaseEvent(_filter) != Dummy.Event)
                WriteKeyword("new");
            WriteKeyword("event");
            WriteTypeName(evnt.Type);
            WriteIdentifier(evnt.Name);

            if (_forCompilation && !evnt.IsAbstract())
            {
                WriteSpace();
                WriteSymbol("{", addSpace: true);
                WriteEventBody("add");
                WriteEventBody("remove");
                WriteSymbol("}");
            }
            else
            {
                WriteSymbol(";");
            }
        }

        private void WriteEventBody(string keyword)
        {
            WriteKeyword(keyword);
            WriteSymbol("{", addSpace: true);
            WriteSymbol("}", addSpace: true);
        }
    }
}
