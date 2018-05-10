using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.ConsoleClient.Filters;

namespace Xunit.ConsoleClient.Project
{
    public class ExtendedXunitProject : XunitProject
    {
        public new ExtendedXunitFilters Filters { get; private set; }
        public ExtendedXunitProject() : base()
        {
            Filters = new ExtendedXunitFilters();
        }
    }
}
