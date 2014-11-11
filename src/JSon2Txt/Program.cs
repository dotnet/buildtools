// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

namespace JSon2Txt
{
    class Program
    {
        enum ParseState
        {
            Input,
            Output,
            End
        }

        static bool ParseArgs(string[] args, Converter converter)
        {
            ParseState state = ParseState.Input;
            foreach (string arg in args)
            {
                switch (state)
                {
                    case ParseState.Input:
                        if (String.Compare(arg, "/out", true) == 0)
                            state = ParseState.Output;
                        else
                            converter.InputFiles.Add(arg);
                        break;
                    case ParseState.Output:
                        converter.OutputFile = arg;
                        state = ParseState.End;
                        break;
                    case ParseState.End:
                        return false;
                }
            }
            return state == ParseState.End;
        }

        static int Main(string[] args)
        {
            Converter converter = new Converter();
            if (!ParseArgs(args, converter))
            {
                Console.WriteLine(String.Format("Usage: {0} <json file list> /out <output file>", Process.GetCurrentProcess().ProcessName));
                return 1;
            }

            try
            {
                converter.Run();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("File = {0}, Line Number = {1}", converter.CurrentFile, converter.CurrentLine);
                Console.WriteLine(e.Message);
                return 2;
            }
        }
    }
}
