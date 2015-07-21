// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Cci.Writers.Syntax
{
    [DebuggerDisplay("{Token}")]
    public class SyntaxToken
    {
        public SyntaxToken(SyntaxTokenType type, string token)
        {
            Type = type;
            Token = token;
        }

        public SyntaxTokenType Type { get; private set; }

        public string Token { get; private set; }

        public override bool Equals(object obj)
        {
            SyntaxToken that = obj as SyntaxToken;
            if (that == null)
                return false;

            return this.Type == that.Type && this.Token == that.Token;
        }

        public override int GetHashCode()
        {
            return this.Type.GetHashCode() ^ this.Token.GetHashCode();
        }
    }
}
