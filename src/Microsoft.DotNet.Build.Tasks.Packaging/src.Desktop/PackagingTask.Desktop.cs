using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public partial class PackagingTask
    {
        static PackagingTask()
        {
            AssemblyResolver.Enable();
        }
    }
}
