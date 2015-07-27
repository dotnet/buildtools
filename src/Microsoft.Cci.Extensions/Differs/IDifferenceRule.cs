// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Cci.Mappings;
using System.Diagnostics.Contracts;

namespace Microsoft.Cci.Differs
{
    public interface IDifferenceRule
    {
        DifferenceType Diff<T>(IDifferences differences, ElementMapping<T> mapping) where T : class;
    }

    public interface IDifferenceRuleMetadata
    {
        bool MdilServicingRule { get; }
    }
}
