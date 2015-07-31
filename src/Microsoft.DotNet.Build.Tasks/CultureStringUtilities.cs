// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace Microsoft.DotNet.Build.Tasks
{
    internal class CultureStringUtilities
    {
        static private Lazy<string[]> cultureInfoStringsLazy = new Lazy<string[]>(GetCultureInfoArray);
        static private string[] cultureInfoStrings
        {
            get
            {
                return cultureInfoStringsLazy.Value;
            }
        }

        internal static bool IsValidCultureString(string cultureString)
        {
            // Note, it does not matter what kind of comparer we use as long as the comparer
            // for Array.Sort() [see PopulateCultureInfoArray()] and Array.BinarySearch() is 
            // the same.  
            bool valid = true;

            if (Array.BinarySearch(cultureInfoStrings, cultureString, StringComparer.OrdinalIgnoreCase) < 0)
            {
                valid = false;
            }

            return valid;
        }

        private static string[] GetCultureInfoArray()
        {
            CultureInfo[] cultureInfos = CultureInfo.GetCultures(CultureTypes.AllCultures);

            var cultureInfoArray = new string[cultureInfos.Length];
            for (int i = 0; i < cultureInfos.Length; i++)
            {
                cultureInfoArray[i] = cultureInfos[i].Name;
            }

            // Note, it does not matter what kind of comparer we use as long as the comparer
            // for Array.BinarySearch() [see ValidateCultureInfoString()] and Array.Sort() is 
            // the same.  
            Array.Sort(cultureInfoArray, StringComparer.OrdinalIgnoreCase);
            return cultureInfoArray;
        }
    }
}
