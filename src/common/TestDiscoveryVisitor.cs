using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;

#if XUNIT_CORE_DLL
namespace Xunit.Sdk
#else
namespace Xunit
#endif
{
    internal class TestDiscoveryVisitor : TestMessageVisitor<IDiscoveryCompleteMessage>
    {
        public TestDiscoveryVisitor()
        {
            TestCases = new List<ITestCase>();
        }

        public List<ITestCase> TestCases { get; private set; }

        protected override bool Visit(ITestCaseDiscoveryMessage discovery)
        {
            List<string> value = new List<string>();
            if (!discovery.TestCase.Traits.TryGetValue("innerloop", out value) || !value.Contains("false"))
                discovery.TestCase.Traits.Add("category", "innerloop");

            TestCases.Add(discovery.TestCase);

            return true;
        }
    }
}