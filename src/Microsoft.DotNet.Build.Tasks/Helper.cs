// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    internal static class Helper
    {
        // normalize the passed name so it can be used as namespace name inside the code
        internal static string NormalizeAssemblyName(string name)
        {
            if (String.IsNullOrEmpty(name))
                return name;

            bool insertUnderscore = Char.IsNumber(name[0]);
            int i = 0;

            while (i < name.Length)
            {
                char c = name[i];
                if (!Char.IsLetter(c) && c != '.' && c != '_' && !Char.IsNumber(c))
                    break;
                i++;
            }

            if (i >= name.Length)
                return insertUnderscore ? "_" + name : name;

            StringBuilder sb = new StringBuilder();
            if (insertUnderscore)
            {
                sb.Append('_');
            }

            for (int j = 0; j < i; j++)
            {
                sb.Append(name[j]);
            }

            sb.Append('_');
            i++;

            while (i < name.Length)
            {
                char c = name[i];
                if (Char.IsLetter(c) || c == '.' || c == '_' || Char.IsNumber(c))
                    sb.Append(c);
                else
                    sb.Append('_');
                i++;
            }

            return sb.ToString();
        }

    }
}
