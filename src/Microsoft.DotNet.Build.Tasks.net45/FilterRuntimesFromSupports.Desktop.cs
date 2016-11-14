using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.DotNet.Build.Tasks
{
    public partial class FilterRuntimesFromSupports
    {
        static FilterRuntimesFromSupports()
        {
            AssemblyResolver.Enable();
        }
    }
}
