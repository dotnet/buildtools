// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Cci.Writers.Syntax
{
    public interface IStyleSyntaxWriter : ISyntaxWriter
    {
        IDisposable StartStyle(SyntaxStyle style, object context);
    }

    public static class StyleSyntaxWriterExtensions
    {
        public static IDisposable StartStyle(this IStyleSyntaxWriter writer, SyntaxStyle style)
        {
            return writer.StartStyle(style, null);
        }
    }
}
