// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;
using System.Linq;
using Xunit.NetCore.Extensions;

namespace Xunit
{
    /// <summary>
    /// Apply this attribute to your test method to specify perf category.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    [TraitDiscoverer("Xunit.NetCore.Extensions.PerfDiscoverer", "Xunit.NetCore.Extensions")]
    public class PerfAttribute : Attribute, ITraitAttribute
    {
        public PerfAttribute() { }
    }
}
