// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Cci.Comparers
{
    public interface ICciComparers
    {
        IEqualityComparer<T> GetEqualityComparer<T>();
        IComparer<T> GetComparer<T>();
    }
}
