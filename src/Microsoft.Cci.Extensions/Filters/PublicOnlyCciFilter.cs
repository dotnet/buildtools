// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.Cci.Extensions;

namespace Microsoft.Cci.Filters
{
    public class PublicOnlyCciFilter : ICciFilter
    {
        public PublicOnlyCciFilter(bool excludeAttributes = true)
        {
            this.ExcludeAttributes = excludeAttributes;
        }

        public bool IncludeForwardedTypes { get; set; }

        public bool ExcludeAttributes { get; set; }

        public virtual bool Include(INamespaceDefinition ns)
        {
            // Only include non-empty namespaces
            return ns.GetTypes(this.IncludeForwardedTypes).Any(Include);
        }

        public virtual bool Include(ITypeDefinition type)
        {
            if (type == null || Dummy.Type == type)
                return false;
            return type.IsVisibleOutsideAssembly();
        }

        public virtual bool Include(ITypeDefinitionMember member)
        {
            if (member == null)
                return false;

            if (!member.ContainingTypeDefinition.IsVisibleOutsideAssembly())
                return false;

            switch (member.Visibility)
            {
                case TypeMemberVisibility.Public:
                    return true;
                case TypeMemberVisibility.Family:
                case TypeMemberVisibility.FamilyOrAssembly:
                    // CCI's version of IsVisibleOutsideAssembly doesn't 
                    // consider protected members as being visible but for
                    // our purposes which is to write CS files that can
                    // be compiled we always need the protected members
                    return true;
            }

            if (!member.IsVisibleOutsideAssembly())
                return false;

            return true;
        }

        public virtual bool Include(ICustomAttribute attribute)
        {
            if (this.ExcludeAttributes)
                return false;

            // Always return the custom attributes if ExcludeAttributes is not set
            // the PublicOnly mainly concerns the types and members.
            return true;
        }
    }
}
