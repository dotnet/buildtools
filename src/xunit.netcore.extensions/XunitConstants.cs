// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    internal struct XunitConstants
    {
        public const string True = "true";
        public const string Category = "category";
        public const string NonWindowsTest = "nonwindowstests";
        public const string NonLinuxTest = "nonlinuxtests";
        public const string NonOSXTest = "nonosxtests";
        public const string Failing = "failing";
        public const string ActiveIssue = "activeissue";
        public const string OuterLoop = "outerloop";
        public const string InnerLoop = "innerloop";
        public const string Perf = "perf";
        public const string Stress = "stress";
        public const string False = "false";
    }
}
