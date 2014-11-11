// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace JSon2Txt
{
    class Converter
    {
        public List<string> InputFiles = new List<string>();
        public string OutputFile;

        //tracking properties
        public String CurrentFile;
        public int CurrentLine;

        Dictionary<string, string> Values = new Dictionary<string, string>();

        public void Run()
        {
            foreach (string file in InputFiles)
                LoadJson(file);

            SaveResult(OutputFile);
        }

        void SaveResult(string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using (StreamWriter writer = File.CreateText(file))
            {
                foreach (string key in Values.Keys)
                    writer.WriteLine(key + "=" + Values[key]);
            }
        }


        void LoadJson(string file)
        {
            CurrentFile = file;
            CurrentLine = 0;

            using (StreamReader reader = File.OpenText(file))
            {
                String str;
                while ((str = reader.ReadLine()) != null)
                {
                    str = str.Trim();
                    int index = str.IndexOf(':');
                    if (str.Length > 0 && str[0] == '\"' && index < str.Length - 2)
                    {
                        StoreValues(str, index);
                    }
                    // Console.WriteLine(s);
                    CurrentLine++;
                }
            }

            CurrentFile = null;
            CurrentLine = 0;
        }

        void StoreValues(string s, int index)
        {
            string leftPart = s.Substring(0, index).Trim();

            if (leftPart.Length < 3 || leftPart[0] != '\"' || leftPart[leftPart.Length - 1] != '\"')
            {
                throw new InvalidDataException("Invalid format in the line: \n" + s);
            }

            StringBuilder lsb = new StringBuilder(leftPart.Length - 2);
            for (var i = 1; i < leftPart.Length - 1; i++)
            {
                switch (leftPart[i])
                {
                    case '&':
                    case '-':
                    case '^':
                    case '[':
                    case ']':
                    case '.':
                        lsb.Append('_');
                        break;
                    default:
                        lsb.Append(leftPart[i]);
                        break;
                }
            }

            leftPart = lsb.ToString();

            string rightPart = s.Substring(index + 1, s.Length - index - 1).Trim();

            if (rightPart[rightPart.Length - 1] == ',')
            {
                rightPart = rightPart.Substring(0, rightPart.Length - 1).Trim();
            }

            if (rightPart.Length < 3 || rightPart[0] != '\"' || rightPart[rightPart.Length - 1] != '\"')
            {
                throw new InvalidDataException("Invalid format in the line: \n" + s);
            }

            StringBuilder rsb = new StringBuilder(rightPart.Length - 2);
            for (var i = 1; i < rightPart.Length - 1; i++)
            {
                if (rightPart[i] == '\"')
                {
                    rsb.Append('\\');
                }
                rsb.Append(rightPart[i]);
            }
            rightPart = rsb.ToString();

            Values.Add(leftPart, rightPart);
        }
    }
}
