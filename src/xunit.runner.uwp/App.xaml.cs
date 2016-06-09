using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xunit;
using Xunit.ConsoleClient;
using Xunit.Shared;

namespace XunitUwpRunner
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        volatile static bool cancel = false;
        
        private string log;
        private async void RunTests(string arguments)
        {
            var reporters = new List<IRunnerReporter>();
            
            string[] args = arguments.Split(new[] { '\x1F' }, StringSplitOptions.RemoveEmptyEntries);
            log = string.Empty;
            log += "Args: " + args + "\n";
            var commandLine = CommandLine.Parse(args);
            if (commandLine.Debug)
            {
                Debugger.Launch();
            }
            var reporterMessageHandler = commandLine.Reporter.CreateMessageHandler(new RunLogger());
            var completionMessages = new ConcurrentDictionary<string, ExecutionSummary>();
            var assembliesElement = new XElement("assemblies");
            
            foreach (var assembly in commandLine.Project.Assemblies)
            {
                if (cancel)
                {
                    return;
                }
                assembly.Configuration.PreEnumerateTheories = false;
                assembly.Configuration.DiagnosticMessages |= commandLine.DiagnosticMessages;
                assembly.Configuration.AppDomain = AppDomainSupport.Denied;
                var discoveryOptions = TestFrameworkOptions.ForDiscovery(assembly.Configuration);
                var executionOptions = TestFrameworkOptions.ForExecution(assembly.Configuration);
                executionOptions.SetDisableParallelization(true);
                
                try
                {
                    using (var xunit = new XunitFrontController(AppDomainSupport.Denied, assembly.AssemblyFilename, assembly.ConfigFilename, assembly.Configuration.ShadowCopyOrDefault))
                    using (var discoveryVisitor = new TestDiscoveryVisitor())
                    {
                        // Discover & filter the tests
                        reporterMessageHandler.OnMessage(new TestAssemblyDiscoveryStarting(assembly, false, false, discoveryOptions));
                        xunit.Find(false, discoveryVisitor, discoveryOptions);
                        discoveryVisitor.Finished.WaitOne();

                        var testCasesDiscovered = discoveryVisitor.TestCases.Count;
                        var filteredTestCases = discoveryVisitor.TestCases.Where(commandLine.Project.Filters.Filter).ToList();
                        var testCasesToRun = filteredTestCases.Count;
                        log += "testCasesToRun: " + testCasesToRun + "\n";

                        reporterMessageHandler.OnMessage(new TestAssemblyDiscoveryFinished(assembly, discoveryOptions, testCasesDiscovered, testCasesToRun));

                        // Run the filtered tests
                        if (testCasesToRun == 0)
                        {
                            completionMessages.TryAdd(Path.GetFileName(assembly.AssemblyFilename), new ExecutionSummary());
                        }
                        else
                        {
                            if (commandLine.Serialize)
                            {
                                filteredTestCases = filteredTestCases.Select(xunit.Serialize).Select(xunit.Deserialize).ToList();
                            }

                            reporterMessageHandler.OnMessage(new TestAssemblyExecutionStarting(assembly, executionOptions));
                            var assemblyElement = new XElement("assembly");

                            IExecutionVisitor resultsVisitor = new XmlAggregateVisitor(reporterMessageHandler, completionMessages, assemblyElement, () => cancel);
                            if (commandLine.FailSkips)
                            {
                                resultsVisitor = new FailSkipVisitor(resultsVisitor);
                            }

                            xunit.RunTests(filteredTestCases, resultsVisitor, executionOptions);
                            
                            log += "finished running tests \n";
                            resultsVisitor.Finished.WaitOne();

                            reporterMessageHandler.OnMessage(new TestAssemblyExecutionFinished(assembly, executionOptions, resultsVisitor.ExecutionSummary));
                            assembliesElement.Add(assemblyElement);
                        }
                    }
                }
                catch (Exception e)
                {
                    assembliesElement = new XElement("error");
                    assembliesElement.Add(e);

                    log += "logged exec errors: " + e + "\n";
                }
            }
            await WriteResults(assembliesElement);
            await WriteLogs(log);
            Application.Current.Exit();
        }

        static async Task WriteResults(XElement data)
        {
            string fname = "testResults.xml";
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file = await folder.CreateFileAsync(fname, Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenStreamForWriteAsync())
            {
                data.Save(stream);
                stream.Flush();
            }
        }

        static async Task WriteLogs(string data)
        {
            string fname = "logs.txt";
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file = await folder.CreateFileAsync(fname, Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenStreamForWriteAsync())
            {
                using (StreamWriter sw = new StreamWriter(stream))
                {
                    await sw.WriteAsync(data);
                }
                stream.Flush();
            }
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }
            // Ensure the current window is active
            Window.Current.Activate();

            // Run tests for assemblies in current directory
            RunTests(e.Arguments);
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
