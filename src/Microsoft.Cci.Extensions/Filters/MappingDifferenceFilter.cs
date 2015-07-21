// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Cci.Mappings;
using Microsoft.Cci.Differs;

namespace Microsoft.Cci.Filters
{
    public class MappingDifferenceFilter : IMappingDifferenceFilter
    {
        private readonly Func<DifferenceType, bool> _include;
        private readonly ICciFilter _filter;

        public MappingDifferenceFilter(Func<DifferenceType, bool> include, ICciFilter filter)
        {
            _include = include;
            _filter = filter;
        }

        public virtual bool Include(AssemblyMapping assembly)
        {
            if (assembly.Namespaces.Any() && Include(assembly.Difference))
                return true;

            return assembly.Namespaces.Any(Include);
        }

        public virtual bool Include(NamespaceMapping ns)
        {
            if (ns.Types.Any() && Include(ns.Difference))
                return true;

            return ns.Types.Any(Include);
        }

        public virtual bool Include(TypeMapping type)
        {
            bool anyIncluded = false;
            for (int i = 0; i < type.ElementCount; i++)
                if (type[i] != null && _filter.Include(type[i]))
                    anyIncluded = true;

            if (!anyIncluded)
                return false;

            if (Include(type.Difference))
                return true;

            if (type.ShouldDiffMembers)
                return type.Members.Any(Include);

            return false;
        }

        public virtual bool Include(MemberMapping member)
        {
            bool anyIncluded = false;
            for (int i = 0; i < member.ElementCount; i++)
                if (member[i] != null && _filter.Include(member[i]))
                    anyIncluded = true;

            if (!anyIncluded)
                return false;

            if (Include(member.Difference))
                return true;
            return false;
        }

        public virtual bool Include(DifferenceType difference)
        {
            return _include(difference);
        }
    }
}
