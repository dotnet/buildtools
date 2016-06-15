using System;
using System.Diagnostics;

namespace Microsoft.DotNet.Execute
{
    public static class Run
    {
        private static System.Diagnostics.Process _process;
        
        public static int ExecuteProcess(string filename, string args = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                _process = new System.Diagnostics.Process();
                _process.StartInfo = psi;

                // Set our event handler to asynchronously read the output.
                _process.OutputDataReceived += new DataReceivedEventHandler(ReadOutputHandler);

                _process.Start();
                _process.BeginOutputReadLine();

                _process.WaitForExit();
                return _process.ExitCode;
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e.Message);
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error in the command: {0}. => {1}", string.Format("{0} {1}", filename, args), e.Message);
                return 1;
            }
        }

        private static void ReadOutputHandler(object sendingProcess,
            DataReceivedEventArgs outLine)
        {
            // Collect the command output.
            if (outLine.Data != null)
            {
                Console.WriteLine(outLine.Data);
            }
        }

    }


            
}
