// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NuGet
{
    public static class PackageIdValidator
    {
        private static readonly Regex _idRegex = new Regex(@"^\w+([_.-]\w+)*$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        internal const int MaxPackageIdLength = 100;

        public static bool IsValidPackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException("packageId");
            }
            return _idRegex.IsMatch(packageId);
        }

        public static void ValidatePackageId(string packageId)
        {
            if (packageId.Length > MaxPackageIdLength)
            {
                // TODO: Resources
                throw new ArgumentException("NuGetResources.Manifest_IdMaxLengthExceeded");
            }

            if (!IsValidPackageId(packageId))
            {
                // TODO: Resources
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "NuGetResources.InvalidPackageId", packageId));
            }
        }
    }
}
