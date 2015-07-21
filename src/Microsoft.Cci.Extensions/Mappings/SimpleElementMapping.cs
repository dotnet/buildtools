// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Cci.Differs;

namespace Microsoft.Cci.Mappings
{
    public class SimpleElementMapping<T> : ElementMapping<T> where T : class
    {
        private readonly DifferenceType _difference;

        public SimpleElementMapping(DifferenceType difference, T element)
            : base(new MappingSettings())
        {
            _difference = difference;
            switch (difference)
            {
                case DifferenceType.Unchanged:
                    AddMapping(0, element);
                    AddMapping(1, element);
                    break;
                case DifferenceType.Removed:
                    AddMapping(0, element);
                    break;
                case DifferenceType.Added:
                    AddMapping(1, element);
                    break;
                default:
                    throw new ArgumentException("Only understand values -1, 0, 1", "diff");
            }
        }

        public override DifferenceType Difference { get { return _difference; } }
    }
}
