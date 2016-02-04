// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Writers.CSharp;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Cci.Mappings;

namespace Microsoft.Cci.Differs.Rules
{
    // @todo: More thinking needed to see whether this is really breaking.
    //[ExportDifferenceRule]
    internal class CannotRemoveGenerics : DifferenceRule
    {
        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            return DiffConstraints(differences, impl, impl.GenericParameters, contract.GenericParameters);
        }

        public override DifferenceType Diff(IDifferences differences, ITypeDefinitionMember impl, ITypeDefinitionMember contract)
        {
            return Diff(differences, impl as IMethodDefinition, contract as IMethodDefinition);
        }

        private DifferenceType Diff(IDifferences differences, IMethodDefinition implMethod, IMethodDefinition contractMethod)
        {
            if (implMethod == null || contractMethod == null)
                return DifferenceType.Unknown;

            return DiffConstraints(differences, implMethod, implMethod.GenericParameters, contractMethod.GenericParameters);
        }

        private DifferenceType DiffConstraints(IDifferences differences, IReference target, IEnumerable<IGenericParameter> implGenericParams, IEnumerable<IGenericParameter> contractGenericParams)
        {
            int beforeCount = differences.Count();
            IGenericParameter[] implParams = implGenericParams.ToArray();
            IGenericParameter[] contractParams = contractGenericParams.ToArray();

            // We shoudn't hit this because the types/members shouldn't be matched up if they have different generic argument lists
            if (implParams.Length != contractParams.Length)
                return DifferenceType.Changed;

            for (int i = 0; i < implParams.Length; i++)
            {
                IGenericParameter implParam = implParams[i];
                IGenericParameter contractParam = contractParams[i];

                if (contractParam.Variance != TypeParameterVariance.NonVariant &&
                    contractParam.Variance != implParam.Variance)
                {
                    differences.AddIncompatibleDifference("CannotChangeVariance",
                        "Variance on generic parameter '{0}' for '{1}' is '{2}' in the implementation but '{3}' in the contract.",
                        implParam.FullName(), target.FullName(), implParam.Variance, contractParam.Variance);
                }

                string implConstraints = string.Join(",", GetConstraints(implParam).OrderBy(s => s));
                string contractConstraints = string.Join(",", GetConstraints(contractParam).OrderBy(s => s));

                if (!string.Equals(implConstraints, contractConstraints))
                {
                    differences.AddIncompatibleDifference("CannotChangeGenericConstraints",
                        "Constraints for generic parameter '{0}' for '{1}' is '{2}' in the implementation but '{3}' in the contract.",
                        implParam.FullName(), target.FullName(), implConstraints, contractConstraints);
                }
            }

            if (differences.Count() != beforeCount)
                return DifferenceType.Changed;

            return DifferenceType.Unknown;
        }

        private IEnumerable<string> GetConstraints(IGenericParameter parameter)
        {
            if (parameter.MustBeValueType)
                yield return "struct";
            else
            {
                if (parameter.MustBeReferenceType)
                    yield return "class";

                if (parameter.MustHaveDefaultConstructor)
                    yield return "new()";
            }

            foreach (var constraint in parameter.Constraints)
            {
                // Skip valuetype becaue we should get it above.
                if (TypeHelper.TypesAreEquivalent(constraint, constraint.PlatformType.SystemValueType) && parameter.MustBeValueType)
                    continue;

                yield return constraint.FullName();
            }
        }
    }
}
