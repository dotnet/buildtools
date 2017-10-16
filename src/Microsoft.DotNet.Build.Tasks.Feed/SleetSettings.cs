using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class SleetSettings
    {
        public List<Source> Sources { get; set; }
    }

    public class Source
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public string Container { get; set; }
        public string ConnectionString { get; set; }
    }
}
