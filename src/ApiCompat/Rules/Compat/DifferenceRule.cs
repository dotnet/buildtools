using System.Composition;

namespace Microsoft.Cci.Differs.Rules
{
    internal abstract class DifferenceRule : Microsoft.Cci.Differs.DifferenceRule
    {
        [Import]
        public IDifferenceOperands Operands { get; set; }

        public string Left => Operands?.Left ?? "contract";
        public string Right => Operands?.Right ?? "implementation";
    }
}
