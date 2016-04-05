using System;

namespace Microsoft.DotNet.Build.Tasks
{
    internal sealed class ExceptionFromResource : Exception
    {
        public string ResourceName { get; private set; }
        public object[] MessageArgs { get; private set; }

        public ExceptionFromResource(string resourceName, params object[] messageArgs)
        {
            ResourceName = resourceName;
            MessageArgs = messageArgs;
        }
    }
}
