using System.Net;
namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public class TLSHandler
    {
        public static void EnableTLS12()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
    }
}