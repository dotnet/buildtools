﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;
using System.Linq;
using Xunit.NetCore.Extensions;

namespace Xunit
{
    /// <summary>
    /// Apply this attribute to your test method to specify Stress category.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    [TraitDiscoverer("Xunit.NetCore.Extensions.StressDiscoverer", "Xunit.NetCore.Extensions")]
    public class StressAttribute : Attribute, ITraitAttribute
    {
        public StressAttribute() { }
    }
}
