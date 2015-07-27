// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Cci.Differs;

namespace Microsoft.Cci.Filters
{
    public class DifferenceFilter<T> : IDifferenceFilter where T : Difference
    {
        public virtual bool Include(Difference difference)
        {
            return difference is T;
        }
    }
}
