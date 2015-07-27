// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Cci.Writers.Syntax;

namespace Microsoft.Cci.Differs
{
    public interface IDiffingService
    {
        IEnumerable<SyntaxToken> GetTokenList(IDefinition definition);
    }
}
