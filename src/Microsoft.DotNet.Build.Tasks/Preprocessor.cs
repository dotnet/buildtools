// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks
{
    public static class Preprocessor
    {
        private const char PreprocessorDelimiter = '$';

        public static void Preprocess(TextReader reader, TextWriter writer, IReadOnlyDictionary<string, string> values)
        {
            // $$ is the escape for a $, which we can just replicate in the algorithm by giving the empty replacement the known value
            var maximumKeyLength = values.Keys.Max<string, int?>(k => k.Length).GetValueOrDefault(0);
            char[] buffer = new char[Math.Max(4096, maximumKeyLength + 2)];

            int charsInBuffer = reader.ReadBlock(buffer, 0, buffer.Length);

            while (charsInBuffer != 0)
            {
                int indexOfDelimiter = Array.IndexOf(buffer, PreprocessorDelimiter, 0, charsInBuffer);

                if (indexOfDelimiter == -1)
                {
                    // If the buffer doesn't contain any delimiters, then we can immediately write it out and move on
                    writer.Write(buffer, 0, charsInBuffer);
                    charsInBuffer = reader.ReadBlock(buffer, 0, buffer.Length);
                }
                else
                {
                    // Write whatever is before the $ and advance up to the $
                    writer.Write(buffer, 0, indexOfDelimiter);
                    Advance(reader, buffer, ref charsInBuffer, charsToAdvance: indexOfDelimiter);

                    // Let's read in the token name
                    var token = new StringBuilder();
                    int position = 1;

                    while (true)
                    {
                        if (position == buffer.Length)
                        {
                            Advance(reader, buffer, ref charsInBuffer, buffer.Length);
                            position = 0;
                        }

                        // If at end, we'll want to fall through to the last case
                        var c = position < charsInBuffer ? buffer[position] : '\0';

                        if (c == PreprocessorDelimiter)
                        {
                            position++;

                            // Is it the escape case?
                            string value;
                            if (token.Length == 0)
                            {
                                writer.Write(PreprocessorDelimiter);
                            }
                            else if (values.TryGetValue(token.ToString(), out value))
                            {
                                writer.Write(value);
                            }
                            else
                            {
                                throw new ExceptionFromResource(nameof(Strings.UnspecifiedToken), PreprocessorDelimiter + token.ToString() + PreprocessorDelimiter);
                            }

                            break;
                        }
                        else if (IsTokenCharacter(c))
                        {
                            token.Append(c);
                            position++;
                        }
                        else
                        {
                            // The token ended prematurely, so we just treat it verbatim and write it out
                            writer.Write(PreprocessorDelimiter);
                            writer.Write(token);

                            break;
                        }
                    }

                    // Advance to the next position to start all over
                    Advance(reader, buffer, ref charsInBuffer, position);
                }
            }
        }

        private static bool IsTokenCharacter(char c)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            return category == UnicodeCategory.LowercaseLetter ||
                   category == UnicodeCategory.UppercaseLetter ||
                   category == UnicodeCategory.TitlecaseLetter ||
                   category == UnicodeCategory.OtherLetter ||
                   category == UnicodeCategory.ModifierLetter ||
                   category == UnicodeCategory.DecimalDigitNumber ||
                   category == UnicodeCategory.ConnectorPunctuation;

        }

        private static void Advance(TextReader reader, char[] buffer, ref int charsInBuffer, int charsToAdvance)
        {
            Debug.Assert(charsToAdvance <= charsInBuffer);

            // Move the remaining characters in the buffer forward
            Array.Copy(sourceArray: buffer, sourceIndex: charsToAdvance, destinationArray: buffer, destinationIndex: 0, length: charsInBuffer - charsToAdvance);
            charsInBuffer -= charsToAdvance;
            charsInBuffer += reader.ReadBlock(buffer, charsInBuffer, buffer.Length - charsInBuffer);
        }
    }
}