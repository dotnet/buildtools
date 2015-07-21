// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter
    {
        public void WriteNamespaceDeclaration(INamespaceDefinition ns)
        {
            WriteKeyword("namespace");
            WriteIdentifier(TypeHelper.GetNamespaceName((IUnitNamespace)ns, NameFormattingOptions.None));
        }
    }
}
