using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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

        public HostedAppxTest(string[] args, XunitProject project, string runnerAppxPath)
        {
            this.originalArgs = args;
            this.project = project;
            this.runnerAppxPath = runnerAppxPath;
        }

        public void Setup()
        {
            tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            object appxFactoryRet;
            NativeMethods.CoCreateInstance(Guids.AppxFactory, null, NativeMethods.CLSCTX_INPROC_SERVER, Guids.IAppxFactory, out appxFactoryRet);
            var appxFactory = (IAppxFactory)appxFactoryRet;
            uint hr;
            var appx = project.Assemblies.SingleOrDefault(a => a.AssemblyFilename.ToLowerInvariant().EndsWith("appx"));
            if (appx != null)
            {
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
                SetupManifestForXunit(manifestPath);
            }
            else
            {
                RecurseCopy(Path.GetDirectoryName(runnerAppxPath), tempDir);
                foreach (var a in project.Assemblies)
                {
                    File.Copy(a.AssemblyFilename, Path.Combine(tempDir, Path.GetFileName(a.AssemblyFilename)));
                }
                manifestPath = Path.Combine(tempDir, "AppxManifest.xml");
                GetManifestInfoFromFile(appxFactory, manifestPath);
            }
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
            var hri = activationManager.ActivateApplication(appUserModelId, this.argsToPass, ACTIVATEOPTIONS.AO_NOERRORUI | ACTIVATEOPTIONS.AO_NOSPLASHSCREEN, out pid);
            var p = Process.GetProcessById((int)pid);
            p.WaitForExit((int)timeout.TotalMilliseconds);
            if (!p.HasExited)
            {
                p.Kill();
            }
            var resultPath = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "Packages", appUserModelId.Substring(0, appUserModelId.IndexOf('!')), "LocalState", "results.xml");
            File.Copy(resultPath, Path.GetFileName(resultPath));
        }

        public void Cleanup()
        {
            var packageManager = new PackageManager();
            var result = packageManager.RemovePackageAsync(packageFullName);
            var completed = new AutoResetEvent(false);
            result.Completed = (waitResult, status) => completed.Set();
            completed.WaitOne();
            Directory.Delete(tempDir, true);
        }

        private void SetupManifestForXunit(string manifestPath)
        {
            var manifest = new XmlDocument();
            manifest.Load(manifestPath);
            manifest["Package"]["Applications"]["Application"].Attributes["Executable"].Value = "XunitUwpRunner.exe";
            manifest["Package"]["Applications"]["Application"].Attributes["EntryPoint"].Value = "XunitUwpRunner.App";
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
