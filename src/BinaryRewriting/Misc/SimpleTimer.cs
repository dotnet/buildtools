// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SimpleTimer
{
    public class ConsoleTimer
    {
        private static Dictionary<String, Stopwatch> s_timers = new Dictionary<string, Stopwatch>();
        private static bool s_verbose = false;
        public static void StartTimer(String identifier)
        {
            if (s_verbose)
                Console.WriteLine("==  Start: {0}...", identifier);
            else
                Console.WriteLine(identifier + "...");

            Stopwatch watch = new Stopwatch();
            s_timers[identifier] = watch;
            watch.Start();
        }
        public static void EndTimer(String identifier)
        {
            Stopwatch watch = s_timers[identifier];
            watch.Stop();
            if (s_verbose)
                Console.WriteLine("==  Done: {0}. Elapsed Time: {1}", identifier, watch.Elapsed);
        }
    }
}
