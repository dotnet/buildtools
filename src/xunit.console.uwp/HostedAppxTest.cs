using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Windows.Management.Deployment;

namespace Xunit.UwpClient
{
    internal class HostedAppxTest
    {
        private string[] originalArgs;
        private string argsToPass;
        private XunitProject project;
        private string tempDir;
        private string runnerAppxPath;
        private string packageFullName = null;
        private string manifestPath = null;
        private string appUserModelId = null;

        private string InstallLocation = null;
        private string PackageSourceDirectory = null;

        public HostedAppxTest(string[] args, XunitProject project, string runnerAppxPath, string installPath)
        {
            this.originalArgs = args;
            this.project = project;
            this.runnerAppxPath = runnerAppxPath;
            this.InstallLocation = installPath;
            this.PackageSourceDirectory = Environment.GetEnvironmentVariable("HELIX_CORRELATION_PAYLOAD");
        }

        public void Setup()
        {
            tempDir = InstallLocation;
            if (!Directory.Exists(tempDir))
            {
                DirectorySecurity securityRules = new DirectorySecurity();
                securityRules.AddAccessRule(new FileSystemAccessRule("Users", FileSystemRights.FullControl, AccessControlType.Allow));

                DirectoryInfo di = Directory.CreateDirectory(tempDir, securityRules);
                Console.WriteLine("Creating directory:" + tempDir);
            }
            object appxFactoryRet;
            NativeMethods.CoCreateInstance(Guids.AppxFactory, null, NativeMethods.CLSCTX_INPROC_SERVER, Guids.IAppxFactory, out appxFactoryRet);
            var appxFactory = (IAppxFactory)appxFactoryRet;
            uint hr;
            var appx = project.Assemblies.SingleOrDefault(a => a.AssemblyFilename.ToLowerInvariant().EndsWith("appx"));
            if (appx != null)
            {
                Console.WriteLine("AppX mode");
                IStream inputStream = null;
                NativeMethods.SHCreateStreamOnFileEx(appx.AssemblyFilename, STGM_CONSTANTS.STGM_READ | STGM_CONSTANTS.STGM_SHARE_EXCLUSIVE, 0, false, null, ref inputStream);
                IAppxPackageReader packageReader;
                appxFactory.CreatePackageReader(inputStream, out packageReader);
                var appxFile = GetManifestInfoFromPackage(appxFactory, packageReader);
                ExtractFile(tempDir, appxFile);
                manifestPath = Path.Combine(tempDir, appxFile.GetName());
                var appxFilesEnumerator = packageReader.GetPayloadFiles();
                var payloadFiles = new List<string>();
                while (appxFilesEnumerator.GetHasCurrent())
                {
                    IAppxFile payloadFile;
                    hr = appxFilesEnumerator.GetCurrent(out payloadFile);
                    ThrowIfFailed(hr);
                    ExtractFile(tempDir, payloadFile);
                    appxFilesEnumerator.MoveNext();
                    var name = payloadFile.GetName();
                    if (name.ToLowerInvariant().EndsWith(".dll") || name.ToLowerInvariant().EndsWith(".exe") && name == Path.GetFileName(name))
                    {
                        payloadFiles.Add(name);
                    }
                }
                argsToPass = string.Join("\x1F", originalArgs.Where(s => !s.ToLowerInvariant().EndsWith(".appx")).Concat(payloadFiles).ToArray());
                CopyXunitDlls(this.runnerAppxPath, this.tempDir);
            }
            else
            {
                Console.WriteLine("DLL mode");
                if (File.Exists("XunitUwpRunner.exe"))
                {
                    tempDir = Directory.GetCurrentDirectory();
                    InstallLocation = tempDir;
                    Console.WriteLine("Runner EXE exists, using this directory: " + tempDir);
                    if (PackageSourceDirectory != null)
                    {
                        Console.WriteLine("Looking for *.dll in {0}", PackageSourceDirectory);
                        foreach (var f in Directory.EnumerateFiles(PackageSourceDirectory, "*.dll", SearchOption.AllDirectories))
                        {
                            var src = Path.Combine(PackageSourceDirectory, f);
                            var dest = Path.Combine(tempDir, Path.GetFileName(f));
                            if (!File.Exists(dest))
                            {
                                Console.WriteLine("Copying {0} to {1}", src, dest);
                                File.Copy(src, dest);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Using temp dir: " + tempDir);
                    RecurseCopy(Path.Combine(Directory.GetCurrentDirectory(), "app"), Path.GetFullPath(tempDir));

                    Console.WriteLine("Install Location: " + tempDir);
                    foreach (var a in project.Assemblies)
                    {
                        Console.WriteLine("Assembly to be tested: " + a.AssemblyFilename);
                        File.Copy(a.AssemblyFilename, Path.Combine(tempDir, Path.GetFileName(a.AssemblyFilename)), true);
                    }
                }
                argsToPass = string.Join("\x1F", originalArgs);
                Console.WriteLine("Arguments passed: " + argsToPass);
            }
            manifestPath = Path.Combine(tempDir, "AppxManifest.xml");
            Console.WriteLine("Using manifest path: " + manifestPath);
            SetupManifestForXunit(manifestPath);
            GetManifestInfoFromFile(appxFactory, manifestPath);
            Console.WriteLine("Registering: " + manifestPath);
            RegisterAppx(new Uri(manifestPath));
        }

        private static void RegisterAppx(Uri manifestUri)
        {
            var packageManager = new PackageManager();
            var result = packageManager.RegisterPackageAsync(manifestUri, null, DeploymentOptions.DevelopmentMode);
            var completed = new AutoResetEvent(false);
            result.Completed = (waitResult, status) => completed.Set();
            completed.WaitOne();
        }

        public void Run(bool debug)
        {
            this.Run(debug, TimeSpan.FromMinutes(10));
        }

        public void Run(bool debug, TimeSpan timeout)
        {
            object returnedComObj = null;
            NativeMethods.CoCreateInstance(Guids.ApplicationActivationManager, null, NativeMethods.CLSCTX_LOCAL_SERVER, Guids.IApplicationActivationManager, out returnedComObj);
            var activationManager = (IApplicationActivationManager)returnedComObj;
            if (debug)
            {
                var packageDebugSettings = new PackageDebugSettings() as IPackageDebugSettings;
                packageDebugSettings.EnableDebugging(packageFullName, null, null);
            }
            IntPtr pid;
            Console.WriteLine("Activating: " + appUserModelId);
            var hri = activationManager.ActivateApplication(appUserModelId, this.argsToPass, ACTIVATEOPTIONS.AO_NOERRORUI | ACTIVATEOPTIONS.AO_NOSPLASHSCREEN, out pid);
            Console.WriteLine("UWP Install HRESULT: " + hri);
            var p = Process.GetProcessById((int)pid);
            Console.WriteLine("Running {0} in process {1} at {2}", p.ProcessName, pid, DateTimeOffset.Now);
            p.WaitForExit((int)timeout.TotalMilliseconds);
            var exitCode = "?";
            if (p.HasExited)
            {
                exitCode = "H";
                try
                {
                    exitCode = p.ExitCode.ToString();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("Finished waiting for {0} at {1}, exit code {2}", pid, DateTimeOffset.Now, exitCode);
            if (!p.HasExited)
            {
                Console.WriteLine("Killing {0}", pid);
                p.Kill();
            }

            var resultPath = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "Packages", appUserModelId.Substring(0, appUserModelId.IndexOf('!')), "LocalState", "testResults.xml");
            Console.WriteLine("Attempting to read {0}", resultPath);
            var destinationPath = Path.Combine(InstallLocation, Path.GetFileName(resultPath));
            Console.WriteLine("Attempting to copy {0} to {1}", resultPath, destinationPath);
            File.Copy(resultPath, destinationPath, true);
            Console.WriteLine("File copied to {0}", destinationPath);
            File.Delete(resultPath);
        }

        public void Cleanup()
        {
            var packageManager = new PackageManager();
            var result = packageManager.RemovePackageAsync(packageFullName, RemovalOptions.PreserveApplicationData);
            var completed = new AutoResetEvent(false);
            result.Completed = (waitResult, status) => completed.Set();
            completed.WaitOne();
        }

        private void SetupManifestForXunit(string manifestPath)
        {
            Console.WriteLine("Updating manifest for testing: " + manifestPath);
            var manifest = new XmlDocument();
            manifest.Load(manifestPath);
            manifest["Package"]["Applications"]["Application"].Attributes["Executable"].Value = "XunitUwpRunner.exe";
            manifest["Package"]["Applications"]["Application"].Attributes["EntryPoint"].Value = "XunitUwpRunner.App";
            var depToRemove = manifest["Package"]["Dependencies"].SelectSingleNode("PackageDependency[@Name='Microsoft.NET.CoreRuntime.1.0']");
            foreach (XmlNode x in manifest["Package"]["Dependencies"])
            {
                Console.WriteLine("Dependency found: {0} -- {1}", x.Attributes["Name"].Value, x.OuterXml);
                if (depToRemove == null && x.Attributes["Name"].Value == "Microsoft.NET.CoreRuntime.1.0")
                {
                    depToRemove = x;
                }
            }
            if (depToRemove != null)
            {
                Console.WriteLine("Removing: " + depToRemove.OuterXml);
                manifest["Package"]["Dependencies"].RemoveChild(depToRemove);
            }
            else
            {
                Console.WriteLine("No CoreRuntime dependency to remove.");
            }
            Console.WriteLine("Saving manifest: " + manifestPath);
            manifest.Save(manifestPath);
        }

        private static void CopyXunitDlls(string runnerAppxPath, string destination)
        {
            File.Copy(runnerAppxPath, Path.Combine(destination, Path.GetFileName(runnerAppxPath)));
            foreach (var f in Directory.GetFiles(Path.GetDirectoryName(runnerAppxPath), "xunit*"))
            {
                if (!File.Exists(Path.Combine(destination, Path.GetFileName(f))))
                {
                    File.Copy(f, Path.Combine(destination, Path.GetFileName(f)));
                }
            }
        }

        private void GetManifestInfo(IAppxManifestReader manifestReader)
        {
            uint hr;
            IAppxManifestPackageId packageId;
            hr = manifestReader.GetPackageId(out packageId);
            ThrowIfFailed(hr);
            hr = packageId.GetPackageFullName(out packageFullName);
            ThrowIfFailed(hr);
            Console.WriteLine("Read package full name: " + packageFullName);
            IAppxManifestApplicationsEnumerator appEnumerator;
            hr = manifestReader.GetApplications(out appEnumerator);
            ThrowIfFailed(hr);
            bool hasCurrent;
            hr = appEnumerator.GetHasCurrent(out hasCurrent);
            ThrowIfFailed(hr);
            if (hasCurrent)
            {
                IAppxManifestApplication app;
                hr = appEnumerator.GetCurrent(out app);
                ThrowIfFailed(hr);
                app.GetAppUserModelId(out appUserModelId);
                Console.WriteLine("Read app user model ID: " + appUserModelId);
            }
        }

        private IAppxFile GetManifestInfoFromPackage(IAppxFactory appxFactory, IAppxPackageReader packageReader)
        {
            var appxFile = packageReader.GetFootprintFile(APPX_FOOTPRINT_FILE_TYPE.MANIFEST);
            IAppxManifestReader manifestReader;
            appxFactory.CreateManifestReader(appxFile.GetStream(), out manifestReader);
            GetManifestInfo(manifestReader);
            return appxFile;
        }

        private void GetManifestInfoFromFile(IAppxFactory appxFactory, string manifestPath)
        {
            Console.WriteLine("Reading manifest info from: " + manifestPath);
            using (var stream = new StreamWrapper(File.OpenRead(manifestPath)))
            {
                IAppxManifestReader manifestReader;
                appxFactory.CreateManifestReader(stream, out manifestReader);
                GetManifestInfo(manifestReader);
            }
        }

        private void ThrowIfFailed(uint hr)
        {
            if (hr != 0)
            {
                throw new COMException("GetManifestInfo error", (int)hr);
            }
        }

        private static void RecurseCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // Copy subdirectories and their contents to new location.
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                RecurseCopy(subdir.FullName, temppath);
            }
        }

        private static void ExtractFile(string tempDir, IAppxFile appxFile)
        {
            var fileName = appxFile.GetName();
            var dest = Path.Combine(tempDir, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            var fileStream = appxFile.GetStream();
            System.Runtime.InteropServices.ComTypes.STATSTG stats;
            fileStream.Stat(out stats, 0);
            using (var wrappedStream = new StreamWrapper(File.OpenWrite(dest)))
            {
                var read = Marshal.AllocHGlobal(sizeof(long));
                var written = Marshal.AllocHGlobal(sizeof(long));
                fileStream.CopyTo(wrappedStream, stats.cbSize, read, written);
                Debug.Assert(Marshal.ReadInt64(read) == Marshal.ReadInt64(written));
                Marshal.FreeHGlobal(read);
                Marshal.FreeHGlobal(written);
            }
        }
    }
}
