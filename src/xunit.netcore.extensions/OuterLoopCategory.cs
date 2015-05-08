// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;
using Xunit.NetCore.Extensions;

namespace Xunit
{
    public class OuterLoopCategory
    {
        private bool _runByDefault;
        private string _name;
        public OuterLoopCategory(bool runByDefault, string name)
        {
            _runByDefault = runByDefault;
            _name = name;
        }

        public static readonly OuterLoopCategory Perf = new OuterLoopCategory(true, XunitConstants.Perf);
        public static readonly OuterLoopCategory Stress = new OuterLoopCategory(true, XunitConstants.Stress);

        internal bool IsRunByDefault()
        {
            return _runByDefault;
        }

        public override string ToString()
        {
            return _name;
        }
    }
}
