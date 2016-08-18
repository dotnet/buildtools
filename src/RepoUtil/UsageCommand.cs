// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace RepoUtil
{
    internal class UsageCommand : ICommand
    {
        public bool Run(TextWriter writer, string[] args)
        {
            Usage(writer);
            return true;
        }

        internal static void Usage(TextWriter writer = null)
        {
            writer = writer ?? Console.Out;
            var text = @"
  verify: check the state of the repo
  consumes: output the conent consumed by this repo
  produces: output the content produced by this repo
  change: change the dependencies.
";
            writer.WriteLine(text);
        }
    }
}
