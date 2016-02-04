// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;
using System;
using System.Diagnostics.Contracts;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class EnumValuesMustMatch : DifferenceRule
    {
        public override DifferenceType Diff(IDifferences differences, ITypeDefinitionMember impl, ITypeDefinitionMember contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (!impl.ContainingTypeDefinition.IsEnum || !contract.ContainingTypeDefinition.IsEnum)
                return DifferenceType.Unknown;

            IFieldDefinition implField = impl as IFieldDefinition;
            IFieldDefinition contractField = contract as IFieldDefinition;

            Contract.Assert(implField != null || contractField != null);

            string implValue = Convert.ToString(implField.Constant.Value);
            string contractValue = Convert.ToString(contractField.Constant.Value);

            // Calling the toString method to compare in since we might have the case where one Enum is type a and the other is type b, but they might still have same value.
            if (implValue != contractValue)
            {
                ITypeReference implValType = impl.ContainingTypeDefinition.GetEnumType();
                ITypeReference contractValType = contract.ContainingTypeDefinition.GetEnumType();

                differences.AddIncompatibleDifference(this,
                    "Enum value '{0}' is ({1}){2} in the implementation but ({3}){4} in the contract.",
                    implField.FullName(), implValType.FullName(), implField.Constant.Value,
                    contractValType.FullName(), contractField.Constant.Value);
                return DifferenceType.Changed;
            }

            return DifferenceType.Unknown;
        }
    }
}
