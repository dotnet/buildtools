using System;
using System.Collections.Generic;
using Xunit.Abstractions;

#if XUNIT_CORE_DLL
namespace Xunit.Sdk
#else
namespace Xunit
#endif
{
    internal class TestDiscoveryVisitor : TestMessageSink
    {
        public TestDiscoveryVisitor()
        {
            TestCases = new List<ITestCase>();

            Discovery.TestCaseDiscoveryMessageEvent += Discovery_TestCaseDiscoveryMessageEvent;
        }

        public List<ITestCase> TestCases { get; private set; }

        private void Discovery_TestCaseDiscoveryMessageEvent(MessageHandlerArgs<ITestCaseDiscoveryMessage> args)
        {
            TestCases.Add(args.Message.TestCase);
        }
    }
}