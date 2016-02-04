// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Writers.CSharp;

namespace Microsoft.Cci.Differs.Rules
{
    // Removed because it appears the *MustExist rules already supercede these.
    [ExportDifferenceRule]
    internal class CannotMakeMoreVisible : DifferenceRule
    {
        public override DifferenceType Diff(IDifferences differences, ITypeDefinitionMember impl, ITypeDefinitionMember contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            // If implementation is public then contract can be any visibility
            if (impl.Visibility == TypeMemberVisibility.Public)
                return DifferenceType.Unknown;

            // If implementation is protected then contract must be protected as well. 
            if (impl.Visibility == TypeMemberVisibility.Family)
            {
                if (contract.Visibility != TypeMemberVisibility.Family)
                {
                    differences.AddIncompatibleDifference(this,
                        "Visibility of member '{0}' is '{1}' in the implementation but '{2}' in the contract.",
                        impl.FullName(), impl.Visibility, contract.Visibility);
                    return DifferenceType.Changed;
                }
            }

            return DifferenceType.Unknown;
        }

        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            // If implementation is public then contract can be any visibility
            if (impl.GetVisibility() == TypeMemberVisibility.Public)
                return DifferenceType.Unknown;

            // If implementation is protected then contract must be protected as well. 
            if (impl.GetVisibility() == TypeMemberVisibility.Family)
            {
                if (contract.GetVisibility() != TypeMemberVisibility.Family)
                {
                    differences.AddIncompatibleDifference(this,
                        "Visibility of type '{0}' is '{1}' in the implementation but '{2}' in the contract.",
                        impl.FullName(), impl.GetVisibility(), contract.GetVisibility());
                    return DifferenceType.Changed;
                }
            }

            return DifferenceType.Unknown;
        }
    }
}
