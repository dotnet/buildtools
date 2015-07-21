// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;

namespace Microsoft.Cci.Mappings
{
    public class NamespaceMapping : ElementMapping<INamespaceDefinition>
    {
        private readonly Dictionary<ITypeDefinition, TypeMapping> _types;

        public NamespaceMapping(MappingSettings settings, bool allowDuplicateMatchingAdds = false)
            : base(settings, allowDuplicateMatchingAdds)
        {
            _types = new Dictionary<ITypeDefinition, TypeMapping>(settings.TypeComparer);
        }

        public IEnumerable<TypeMapping> Types { get { return _types.Values; } }

        protected override void OnMappingAdded(int index, INamespaceDefinition element)
        {
            foreach (var type in element.GetTypes(this.Settings.IncludeForwardedTypes).Where(this.Filter.Include))
            {
                TypeMapping mapping;
                if (!_types.TryGetValue(type, out mapping))
                {
                    mapping = new TypeMapping(this.Settings);
                    _types.Add(type, mapping);
                }
                mapping.AddMapping(index, type);
            }
        }
    }
}
