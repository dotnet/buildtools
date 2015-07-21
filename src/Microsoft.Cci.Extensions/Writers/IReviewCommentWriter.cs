// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Cci.Writers
{
    public interface IReviewCommentWriter
    {
        void WriteReviewComment(string author, string text);
    }
}
