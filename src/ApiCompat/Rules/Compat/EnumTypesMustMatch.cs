using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class EnumTypesMustMatch : DifferenceRule
    {
        [Import]
        private IEqualityComparer<ITypeReference> _typeComparer = null;

        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (!impl.IsEnum || !contract.IsEnum)
                return DifferenceType.Unknown;

            ITypeReference implType = impl.GetEnumType();
            ITypeReference contractType = contract.GetEnumType();

            if (!_typeComparer.Equals(implType, contractType))
            {
                differences.AddTypeMismatchDifference(this, implType, contractType,
                    "Enum type for '{0}' is '{1}' in implementation but '{2}' in the contract.",
                    impl.FullName(), implType.FullName(), contractType.FullName());
                return DifferenceType.Changed;
            }

            return DifferenceType.Unknown;
        }
    }
}
