// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;
using System.Linq;
using System.Collections.Generic;
using System.Composition;
using Microsoft.Cci.Comparers;
using System;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class InterfacesShouldHaveSameMembers : DifferenceRule
    {
        public override DifferenceType Diff(IDifferences differences, ITypeDefinitionMember impl, ITypeDefinitionMember contract)
        {
            if (contract != null && impl == null)
            {
                if (contract.ContainingTypeDefinition.IsInterface)
                {
                    differences.AddIncompatibleDifference(this, "Contract interface member '{0}' is not in the implementation.", contract.FullName());
                    return DifferenceType.Changed;
                }
            }

            if (impl != null && contract == null)
            {
                if (impl.ContainingTypeDefinition.IsInterface)
                {
                    differences.AddIncompatibleDifference(this, "Implementation interface member '{0}' is not in the contract.", impl.FullName());
                    return DifferenceType.Changed;
                }
            }

            return base.Diff(differences, impl, contract);
        }
    }
}
