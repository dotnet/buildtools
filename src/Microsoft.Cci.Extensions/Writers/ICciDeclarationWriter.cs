// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Cci.Writers
{
    public interface ICciDeclarationWriter
    {
        void WriteDeclaration(IDefinition definition);
        void WriteAttribute(ICustomAttribute attribute);
    }
}
