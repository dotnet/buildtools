// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Composition;
using Microsoft.Cci.Extensions;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class CannotRemoveBaseTypeOrInterface : DifferenceRule
    {
        [Import]
        public IEqualityComparer<ITypeReference> _typeComparer { get; set; } = null;

        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (AddedBaseType(differences, impl, contract) ||
                AddedInterface(differences, impl, contract))
                return DifferenceType.Changed;

            return DifferenceType.Unknown;
        }

        private bool AddedBaseType(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            // For interfaces we rely only on the AddedInterface check
            if (impl.IsInterface || contract.IsInterface)
                return false;

            // Base types must be in the same order so we have to compare them in order
            List<ITypeReference> implBaseTypes = new List<ITypeReference>(impl.GetAllBaseTypes());

            int lastIndex = 0;
            foreach (var contractBaseType in contract.GetAllBaseTypes())
            {
                lastIndex = implBaseTypes.FindIndex(lastIndex, item1BaseType => _typeComparer.Equals(item1BaseType, contractBaseType));

                if (lastIndex < 0)
                {
                    differences.AddIncompatibleDifference(this,
                        "Type '{0}' does not inherit from base type '{1}' in the implementation but it does in the contract.",
                        contract.FullName(), contractBaseType.FullName());
                    return true;
                }
            }

            return false;
        }

        private bool AddedInterface(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            // Interfaces can be in any order so use a HashSet
            HashSet<ITypeReference> implInterfaces = new HashSet<ITypeReference>(impl.GetAllInterfaces(), _typeComparer);

            foreach (var contractInterface in contract.GetAllInterfaces())
            {
                // Ignore internal interfaces
                if (!contractInterface.IsVisibleOutsideAssembly())
                    continue;

                if (!implInterfaces.Contains(contractInterface))
                {
                    differences.AddIncompatibleDifference(this,
                        "Type '{0}' does not implement interface '{1}' in the implementation but it does in the contract.",
                            contract.FullName(), contractInterface.FullName());
                    return true;
                }
            }

            return false;
        }
    }
}
