// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.CodeAnalysis.Analyzers
{
    public abstract class BaseAnalyzer : DiagnosticAnalyzer
    {
        private static HashSet<string> s_disabledAnalyzers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static volatile bool s_disabledAnalyzersInitialized = false;
        private const string ConfigFileName = @"disabledAnalyzers.config";

        public sealed override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(InitializeAnalyzer);
        }

        private void InitializeAnalyzer(CompilationStartAnalysisContext context)
        {
            EnsureConfigFileLoaded(context.Options);

            // Disable analyzers if they are in that file
            if (s_disabledAnalyzers.Contains(GetType().Name))
            {
                return;
            }

            OnCompilationStart(context);
        }

        /// <summary>
        /// This is going to be called only if the analyzer was not disabled
        /// </summary>
        /// <param name="context"></param>
        public abstract void OnCompilationStart(CompilationStartAnalysisContext context);

        /// <summary>
        /// We should not have multiple analyzers process the same file. Instead, we store it in a hashset
        /// </summary>
        /// <param name="options"></param>
        private static void EnsureConfigFileLoaded(AnalyzerOptions options)
        {
            if (s_disabledAnalyzersInitialized == false)
            {
                lock (s_disabledAnalyzers)
                {
                    if (s_disabledAnalyzersInitialized == false)
                    {
                        try
                        {
                            var configFile = options.AdditionalFiles.FirstOrDefault(file => file.Path.Contains(ConfigFileName));

                            if (configFile == null)
                            {
                                return;
                            }
                            foreach (var line in configFile.GetText().Lines)
                            {
                                s_disabledAnalyzers.Add(line.ToString());
                            }
                        }
                        finally
                        {
                            s_disabledAnalyzersInitialized = true;
                        }
                    }
                }
            }
        }

    }
}
