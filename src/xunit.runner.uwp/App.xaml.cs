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

namespace XunitUwpRunner
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        volatile static bool cancel = false;

        private async void RunTests(string arguments)
        {
            var reporters = await GetAvailableRunnerReporters();
            var commandLine = CommandLine.Parse(reporters, arguments.Split(new[] { '\x1F' }, StringSplitOptions.RemoveEmptyEntries));
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
                            resultsVisitor.Finished.WaitOne();

                            reporterMessageHandler.OnMessage(new TestAssemblyExecutionFinished(assembly, executionOptions, resultsVisitor.ExecutionSummary));
                            assembliesElement.Add(assemblyElement);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }
            await WriteResults(assembliesElement);
            Application.Current.Exit();
        }

        static async Task WriteResults(XElement data)
        {
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file = await folder.CreateFileAsync("results.xml", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenStreamForWriteAsync())
            {
                data.Save(stream);
                stream.Flush();
            }
        }


        static async Task<List<IRunnerReporter>> GetAvailableRunnerReporters()
        {
            var result = new List<IRunnerReporter>();
            var folder = Package.Current.InstalledLocation;
            var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            var candidates = files.Where(f => f.Name.EndsWith("dll") || f.Name.EndsWith("exe")).Select(f => f.Name);

            foreach (var dllFile in candidates)
            {
                Type[] types;

                try
                {
                    var assembly = Assembly.Load(new AssemblyName { Name = dllFile });
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null || type == typeof(DefaultRunnerReporter) || !type.GetInterfaces().Any(t => t == typeof(IRunnerReporter)))
                    {
                        continue;
                    }
                    var ctor = type.GetConstructor(new Type[0]);
                    if (ctor == null)
                    {
                        continue;
                    }

                    result.Add((IRunnerReporter)ctor.Invoke(new object[0]));
                }
            }

            return result;
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
