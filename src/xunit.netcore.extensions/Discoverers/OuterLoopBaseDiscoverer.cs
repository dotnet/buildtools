// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    /// This class discovers all of the tests and test classes that have
    /// applied the OuterLoop attribute
    /// </summary>
    public class OuterLoopBaseDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            IEnumerable<object> ctorArgs = traitAttribute.GetConstructorArguments();
            if (ctorArgs.Count() == 1)
            {
                OuterLoopCategory category = (OuterLoopCategory)ctorArgs.First();
                if (category.IsRunByDefault())
                    yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.OuterLoop);
                yield return new KeyValuePair<string, string>(XunitConstants.Category, category.ToString());
            }
            else
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.OuterLoop);
            // Pass (outerloop, true) to exclude this test from innerloop.
            yield return new KeyValuePair<string, string>(XunitConstants.OuterLoop, XunitConstants.True);
        }
    }
}

