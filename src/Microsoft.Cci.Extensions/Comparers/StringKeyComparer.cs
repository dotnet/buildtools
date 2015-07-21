// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Cci.Comparers
{
    public class StringKeyComparer<T> : IComparer<T>, IEqualityComparer<T>
    {
        private readonly Func<T, string> _getKey;

        public StringKeyComparer()
            : this(null)
        {
        }

        public StringKeyComparer(Func<T, string> getKey)
        {
            if (getKey == null)
                _getKey = t => t.ToString();
            else
                _getKey = getKey;
        }

        public bool Equals(T x, T y)
        {
            return Compare(x, y) == 0;
        }

        public int GetHashCode(T obj)
        {
            return GetKey(obj).GetHashCode();
        }

        public virtual string GetKey(T t)
        {
            return _getKey(t);
        }

        public virtual int Compare(T x, T y)
        {
            return string.Compare(GetKey(x), GetKey(y));
        }
    }
}
