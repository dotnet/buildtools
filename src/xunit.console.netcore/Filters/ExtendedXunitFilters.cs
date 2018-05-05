using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Xunit.ConsoleClient.Filters
{
    /// <summary>
    /// Wrapper class, which hides XunitFilters' Filter method and adds logic to exclude methods or namespaces
    /// </summary>
    public class ExtendedXunitFilters : XunitFilters
    {
        public HashSet<string> ExcludedMethods { get; private set; }
        public HashSet<string> ExcludedClasses { get; private set; }
        public HashSet<string> ExcludedNamespaces { get; private set; }

        public ExtendedXunitFilters() : base()
        {
            ExcludedMethods = new HashSet<string>();
            ExcludedClasses = new HashSet<string>();
            ExcludedNamespaces = new HashSet<string>();
        }

        /// <summary>
        /// Determine whether a passed test case should run
        /// </summary>
        /// <param name="testCase">Test case to filter</param>
        /// <returns>Boolean - True runs the test case, False skips</returns>
        public new bool Filter(ITestCase testCase)
        {
            // Exclusions supersede inclusions - i.e. if a method/class/namespace is both included and excluded it won't run
            return FilterExcludedMethodsAndClasses(testCase) && base.Filter(testCase);
        }

        bool FilterExcludedMethodsAndClasses(ITestCase testCase)
        {
            // If no explicit exclusions have been defined, return true
            if (ExcludedMethods.Count == 0 && ExcludedClasses.Count == 0 && ExcludedNamespaces.Count == 0)
                return true;

            if (ExcludedClasses.Count != 0 && ExcludedClasses.Contains(testCase.TestMethod.TestClass.Class.Name))
                return false;

            var methodName = $"{testCase.TestMethod.TestClass.Class.Name}.{testCase.TestMethod.Method.Name}";

            if (ExcludedMethods.Count != 0 && ExcludedMethods.Contains(methodName))
                return false;

            if (ExcludedNamespaces.Count != 0 && ExcludedNamespaces.Any(a => testCase.TestMethod.TestClass.Class.Name.StartsWith($"{a}.", StringComparison.Ordinal)))
                return false;

            return true;
        }

    }
}
