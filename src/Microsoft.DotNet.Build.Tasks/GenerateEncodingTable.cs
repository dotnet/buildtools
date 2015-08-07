using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateEncodingTable : Task
    {
        [Required]
        public string inputPath { get; set; }

        public override bool Execute()
        {
            return true;
        }
    }
}
