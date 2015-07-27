// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if COREFX
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Diagnostics
{
    // Below taken from Reference Source
    // Outputs trace messages to the console.
    public class ConsoleTraceListener : TextWriterTraceListener
    {
        public ConsoleTraceListener()
            : base(Console.Out)
        { }

        public ConsoleTraceListener(bool useErrorStream)
            : base(useErrorStream ? Console.Error : Console.Out)
        { }
    }
}
#endif
