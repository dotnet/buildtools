using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ExecWithMutex : Exec
    {
        [Required]
        public string MutexName
        {
            get;
            set;
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            string actualMutexName = MutexName.Replace(Path.DirectorySeparatorChar, '_');
            bool created = false;

            using (Mutex mutex = new Mutex(true, actualMutexName, out created))
            {
                try
                {
                    if (!created)
                    {
                        mutex.WaitOne();
                    }
                    return base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }
}
