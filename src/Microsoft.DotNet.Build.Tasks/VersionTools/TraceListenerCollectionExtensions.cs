// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public static class TraceListenerCollectionExtensions
    {
        /// <summary>
        /// Adds listeners to Trace that pass output into the given msbuild logger, making Trace
        /// calls visible in build output. VersionTools, for example, uses Trace. Returns the
        /// listeners to pass to RemoveMsBuildTraceListeners when the code using Trace is complete.
        /// </summary>
        public static MsBuildTraceListener[] AddMsBuildTraceListeners(
            this TraceListenerCollection listenerCollection,
            TaskLoggingHelper log)
        {
            var newListeners = new[]
            {
                TraceEventType.Error,
                TraceEventType.Warning,
                TraceEventType.Critical,
                TraceEventType.Information,
                TraceEventType.Verbose
            }.Select(t => new MsBuildTraceListener(log, t)).ToArray();

            listenerCollection.AddRange(newListeners);
            return newListeners;
        }

        /// <summary>
        /// Removes the given listeners from Trace. This cleans up static state to avoid
        /// accidentally routing unrelated Trace data to msbuild logs.
        /// 
        /// Calls Flush on each listener in case the last call was Write.
        /// </summary>
        public static void RemoveMsBuildTraceListeners(
            this TraceListenerCollection listenerCollection,
            IEnumerable<MsBuildTraceListener> traceListeners)
        {
            foreach (MsBuildTraceListener listener in traceListeners)
            {
                listenerCollection.Remove(listener);
                listener.Flush();
            }
        }
    }
}