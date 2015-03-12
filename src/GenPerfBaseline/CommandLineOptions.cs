// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PowerArgs;
using System;
using System.Collections.Generic;

namespace GenPerfBaseline
{
    class CommandLineOptions
    {
        [ArgDescription("Pattern of input files to merge."), ArgRequired]
        public string Input { get; set; }

        [ArgDescription("Destination output file."), ArgRequired]
        public string Output { get; set; }
    }
}
