using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.DotNet.Execute
{
    public static class Run
    {
        private static System.Diagnostics.Process _process;
        private static bool _exited = false;

        public static int ExecuteProcess(string filename, string args = null)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                _process = new System.Diagnostics.Process();
                _process.StartInfo = psi;
                _process.EnableRaisingEvents = true;

                // Set the event handler to asynchronously read the output.
                _process.OutputDataReceived += new DataReceivedEventHandler(ReadOutputHandler);
                
                _process.Start();
                _process.BeginOutputReadLine();
                string errorOutput = _process.StandardError.ReadToEnd();

                _process.WaitForExit();
                Console.Error.WriteLine(errorOutput);

                const int SLEEP_AMOUNT = 1000;
                while (!_exited)
                {
                    Thread.Sleep(SLEEP_AMOUNT);
                }
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
            else
            {
                _exited = true;
            }
        }
    }
}
