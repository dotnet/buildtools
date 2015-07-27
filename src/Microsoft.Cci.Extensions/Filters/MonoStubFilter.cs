// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
