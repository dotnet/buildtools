using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Cci.Differs
{
    /// <summary>
    /// Names for the left and right operands of a difference
    /// </summary>
    public interface IDifferenceOperands
    {
        /// <summary>
        /// Name of left operand of a difference operation.  Typically called a contract or reference.
        /// </summary>
        string Left { get; }
        /// <summary>
        /// Name of right operand of a difference operation.  Typically called an implemenation.
        /// </summary>
        string Right { get; }
    }

    public class DifferenceOperands : IDifferenceOperands
    {
        public string Left { get; set; }

        public string Right { get; set; }
    }
}
