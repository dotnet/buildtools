using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class SleetSource
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public string Container { get; set; }
        public string ConnectionString { get; set; }
        public string FeedSubPath { get; set; }
    }
}
