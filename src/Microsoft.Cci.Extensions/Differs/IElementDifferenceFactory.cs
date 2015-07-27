// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Cci.Mappings;

namespace Microsoft.Cci.Differs
{
    public interface IElementDifferenceFactory
    {
        IDifferences GetDiffer<T>(ElementMapping<T> element) where T : class;
    }
}
