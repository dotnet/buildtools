// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Microsoft.Cci;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Mappings;
using Microsoft.Cci.Traversers;
using System;


namespace Microsoft.Cci.Writers
{
    public class DifferenceWriter : DifferenceTraverser, ICciDifferenceWriter
    {
        private readonly List<Difference> _differences;
        private readonly TextWriter _writer;
        private int _totalDifferences = 0;
        public static int ExitCode { get; set; }

        public DifferenceWriter(TextWriter writer, MappingSettings settings, IDifferenceFilter filter)
            : base(settings, filter)
        {
            _writer = writer;
            _differences = new List<Difference>();
        }

        public void Write(string oldAssembliesName, IEnumerable<IAssembly> oldAssemblies, string newAssembliesName, IEnumerable<IAssembly> newAssemblies)
        {
            this.Visit(oldAssemblies, newAssemblies);

            if (!this.Settings.GroupByAssembly)
            {
                if (_differences.Count > 0)
                {
                    string header = string.Format("Compat issues between implementation set {0} and contract set {1}:", oldAssembliesName, newAssembliesName);
                    OutputDifferences(header, _differences);
                    _totalDifferences += _differences.Count;
                    _differences.Clear();
                }
            }

            _writer.WriteLine("Total Issues: {0}", _totalDifferences);
            _totalDifferences = 0;
        }

        public override void Visit(AssemblyMapping mapping)
        {
            Contract.Assert(_differences.Count == 0);

            base.Visit(mapping);

            if (this.Settings.GroupByAssembly)
            {
                if (_differences.Count > 0)
                {
                    string header = string.Format("Compat issues with assembly {0}:", mapping.Representative.Name.Value);
                    OutputDifferences(header, _differences);
                    _totalDifferences += _differences.Count;
                    _differences.Clear();
                }
            }
        }

        private void OutputDifferences(string header, IEnumerable<Difference> differences)
        {
            _writer.WriteLine(header);

            foreach (var diff in differences)
                _writer.WriteLine(diff.ToString());
        }

        public override void Visit(Difference difference)
        {
            _differences.Add(difference);

            // For now use this to set the ExitCode to 2 if there are any differences
            DifferenceWriter.ExitCode = 2;
        }
    }
}
