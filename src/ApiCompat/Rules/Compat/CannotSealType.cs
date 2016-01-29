using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class CannotSealType : DifferenceRule
    {
        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            if (impl.IsEffectivelySealed() && !contract.IsEffectivelySealed())
            {
                differences.AddIncompatibleDifference(this,
                    "Type '{0}' is sealed in the implementation but not sealed in the contract.", impl.FullName());

                return DifferenceType.Changed;
            }

            return DifferenceType.Unknown;
        }
    }
}
