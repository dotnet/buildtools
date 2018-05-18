// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    /// This class discovers all of the tests and test classes that have
    /// applied the ConditionalClass attribute
    /// </summary>
    public class ConditionalClassDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            // Parse the traitAttribute. We make sure it contains two parts:
            // 1. Type 2. nameof(conditionMemberName)
            object[] conditionArguments = traitAttribute.GetConstructorArguments().ToArray();
            Debug.Assert(conditionArguments.Count() == 2);

            // If evaluated to false, entirely skip the test class.
            if (!ConditionalTestDiscoverer.EvaluateParameter(conditionArguments))
            {
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing);
            }
        }
    }
}
