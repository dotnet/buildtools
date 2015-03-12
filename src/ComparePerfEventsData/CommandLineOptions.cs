// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PowerArgs;
using System;
using System.Collections.Generic;

namespace ComparePerfEventsData
{
    class CommandLineOptions
    {
        [ArgDescription("Baseline data file."), ArgExistingFile, ArgRequired]
        public string Baseline { get; set; }

        [ArgDescription("Live data file."), ArgExistingFile, ArgRequired]
        public string Live { get; set; }
    }
}
