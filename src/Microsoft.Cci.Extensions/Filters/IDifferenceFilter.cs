// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Cci.Differs;

namespace Microsoft.Cci.Filters
{
    public interface IDifferenceFilter
    {
        bool Include(Difference difference);
    }
}
