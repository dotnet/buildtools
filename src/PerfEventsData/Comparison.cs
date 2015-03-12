// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace PerfEventsData
{
    /// <summary>
    /// Behavior when comparing baseline and live data.
    /// </summary>
    public enum Comparison
    {
        /// <summary>
        /// If live is less than baseline this is good; this is the default.
        /// </summary>
        LowerTheBetter,

        /// <summary>
        /// If live is greater than baseline this is good.
        /// </summary>
        GreaterTheBetter
    }
}
