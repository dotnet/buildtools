// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Composition;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class DelegatesMustMatch : DifferenceRule
    {
        [Import]
        public IEqualityComparer<ITypeReference> _typeComparer { get; set; } = null;

        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (!impl.IsDelegate || !contract.IsDelegate)
                return DifferenceType.Unknown;

            IMethodDefinition implMethod = impl.GetInvokeMethod();
            IMethodDefinition contractMethod = contract.GetInvokeMethod();

            Contract.Assert(implMethod != null && contractMethod != null);

            if (!ReturnTypesMatch(differences, implMethod, contractMethod) ||
                !ParamNamesAndTypesMatch(differences, implMethod, contractMethod))
                return DifferenceType.Changed;

            return DifferenceType.Unknown;
        }

        private bool ReturnTypesMatch(IDifferences differences, IMethodDefinition implMethod, IMethodDefinition contractMethod)
        {
            ITypeReference implReturnType = implMethod.GetReturnType();
            ITypeReference contractReturnType = contractMethod.GetReturnType();

            if (implReturnType == null || contractReturnType == null)
                return true;

            if (!_typeComparer.Equals(implReturnType, contractReturnType))
            {
                differences.AddTypeMismatchDifference("DelegateReturnTypesMustMatch", implReturnType, contractReturnType,
                    "Return type on delegate '{0}' is '{1}' in the implementation but '{2}' in the contract.",
                    implMethod.ContainingType.FullName(), implReturnType.FullName(), contractReturnType.FullName());
                return false;
            }

            return true;
        }

        private bool ParamNamesAndTypesMatch(IDifferences differences, IMethodDefinition implMethod, IMethodDefinition contractMethod)
        {
            int paramCount = implMethod.ParameterCount;

            Contract.Assert(paramCount == contractMethod.ParameterCount);

            IParameterDefinition[] implParams = implMethod.Parameters.ToArray();
            IParameterDefinition[] contractParams = contractMethod.Parameters.ToArray();

            bool match = true;
            for (int i = 0; i < paramCount; i++)
            {
                IParameterDefinition implParam = implParams[i];
                IParameterDefinition contractParam = contractParams[i];

                if (!implParam.Name.Value.Equals(contractParam.Name.Value))
                {
                    differences.AddIncompatibleDifference("DelegateParamNameMustMatch",
                        "Parameter name on delegate '{0}' is '{1}' in the implementation but '{2}' in the contract.",
                        implMethod.ContainingType.FullName(), implParam.Name.Value, contractParam.Name.Value);
                    match = false;
                }

                if (!_typeComparer.Equals(implParam.Type, contractParam.Type))
                {
                    differences.AddTypeMismatchDifference("DelegateParamTypeMustMatch", implParam.Type, contractParam.Type,
                        "Type for parameter '{0}' on delegate '{1}' is '{2}' in the implementation but '{3}' in the contract.",
                        implParam.Name.Value, implMethod.ContainingType.FullName(), implParam.Type.FullName(), contractParam.Type.FullName());
                    match = false;
                }
            }
            return match;
        }
    }
}
