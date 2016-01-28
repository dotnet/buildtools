// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Cci.Filters
{
    /// <summary>
    /// Mono generates stubs for all APIs available in .NET that do not have an available implementation.  The convention to 
    /// mark an API as a stub is to adorn it with the attribute "System.MonoTODOAtribute".
    /// </summary>
    /// <see cref="http://www.mono-project.com/community/contributing/coding-guidelines/#missing-implementation-bits"/>
    public class MonoStubFilter : AttributeMarkedFilter
    {
        public MonoStubFilter() :
            base("System.MonoTODOAttribute")
        { }
    }
}
