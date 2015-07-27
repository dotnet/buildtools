// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Writers.CSharp;

namespace Microsoft.Cci.Comparers
{
    public class AttributeComparer : StringKeyComparer<ICustomAttribute>
    {
        private readonly CSDeclarationHelper _helper;

        public AttributeComparer()
            : this(new IncludeAllFilter(), false)
        {
        }

        public AttributeComparer(ICciFilter filter, bool forCompilation)
        {
            _helper = new CSDeclarationHelper(filter, forCompilation);
        }

        public override string GetKey(ICustomAttribute c)
        {
            return _helper.GetString(c);
        }
    }
}
