// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Differs.Rules
{
    [ExportDifferenceRule]
    internal class TypeCannotChangeClassification : DifferenceRule
    {
        public override DifferenceType Diff(IDifferences differences, ITypeDefinition impl, ITypeDefinition contract)
        {
            if (impl == null || contract == null)
                return DifferenceType.Unknown;

            string implObjType = GetObjectType(impl);
            string contractObjType = GetObjectType(contract);

            if (implObjType != contractObjType)
            {
                differences.AddIncompatibleDifference(this,
                    "Type '{0}' is a '{1}' in the implementation but is a '{2}' in the contract.",
                    impl.FullName(), implObjType, contractObjType);

                return DifferenceType.Changed;
            }

            if (contract.Attributes.HasIsReadOnlyAttribute() && !impl.Attributes.HasIsReadOnlyAttribute())
            {
                differences.AddIncompatibleDifference(this,
                    "Type '{0}' is marked as readonly in the contract so it must also be marked readonly in the implementation.",
                    impl.FullName());

                return DifferenceType.Changed;
            }

            return DifferenceType.Unknown;
        }

        private string GetObjectType(ITypeDefinition type)
        {
            if (type.IsClass)
                return "class";

            if (type.IsValueType)
            {
                if (type.Attributes.HasIsByRefLikeAttribute())
                    return "ref struct";

                return "struct";
            }

            if (type.IsInterface)
                return "interface";

            if (type.IsDelegate)
                return "delegate";

            throw new System.NotSupportedException(string.Format("Only support types that are class, struct, or interface. {0}", type.GetType()));
        }
    }
}
