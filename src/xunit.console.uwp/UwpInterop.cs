using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.CompilerServices;

namespace Xunit.UwpClient
{
        internal enum ACTIVATEOPTIONS
        {
            AO_NONE = 0x00000000,
            AO_DESIGNMODE = 0x00000001,
            AO_NOERRORUI = 0x00000002,
            AO_NOSPLASHSCREEN = 0x00000004,
        }

        internal enum PACKAGE_EXECUTION_STATE
        {
            PES_UNKNOWN = 0,
            PES_RUNNING = 1,
            PES_SUSPENDING = 2,
            PES_SUSPENDED = 3,
            PES_TERMINATED = 4
        }

        [ComImport]
        [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IApplicationActivationManager
        {
            [PreserveSig]
            int ActivateApplication(
                [In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                [In, MarshalAs(UnmanagedType.LPWStr)] string arguments,
                [In] ACTIVATEOPTIONS options,
                [Out] out IntPtr processId);

            [PreserveSig]
            int ActivateForFile(
                [In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                [In] object itemArray,      //IShellItemArray
                [In, MarshalAs(UnmanagedType.LPWStr)] string verb,
                [Out] out IntPtr processId);

            [PreserveSig]
            int ActivateForProtocol(
                [In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                [In] object itemArray,      //IShellItemArray
                [Out] out IntPtr processId);
        }

        [ComImport]
        [Guid("1BB12A62-2AD8-432B-8CCF-0C2C52AFCD5B")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPackageExecutionStateChangeNotification
        {
            [PreserveSig]
            int OnStateChanged([In, MarshalAs(UnmanagedType.LPWStr)] string pszPackageFullName, [In] PACKAGE_EXECUTION_STATE pesNewState);
        }

        [ComImport,
             Guid("B1AEC16F-2383-4852-B0E9-8F0B1DC66B4D")]
        public class PackageDebugSettings
        {
        }

        [ComImport]
        [Guid("F27C3930-8029-4AD1-94E3-3DBA417810C1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPackageDebugSettings
        {
            [PreserveSig]
            int EnableDebugging(
                [In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName,
                [In, MarshalAs(UnmanagedType.LPWStr)] string debuggerCommandLine,
                [In, MarshalAs(UnmanagedType.LPWStr)] string environment);
            [PreserveSig]
            int DisableDebugging([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
            [PreserveSig]
            int Suspend([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
            [PreserveSig]
            int Resume([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
            [PreserveSig]
            int TerminateAllProcesses([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
            [PreserveSig]
            int SetTargetSessionId([In] UIntPtr sessionId);
            [PreserveSig]
            int EnumerateBackgroundTasks(
                [In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName,
                [Out] UIntPtr taskCount,
                [Out, MarshalAs(UnmanagedType.LPStruct)] Guid taskIds,
                [Out, MarshalAs(UnmanagedType.LPWStr)] string taskNames);
            [PreserveSig]
            int ActivateBackgroundTask([In, MarshalAs(UnmanagedType.LPStruct)] Guid taskId);
            [PreserveSig]
            int StartServicing([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
            [PreserveSig]
            int StopServicing([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
            [PreserveSig]
            int StartSessionRedirection([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName, [In] uint sessionId);
            [PreserveSig]
            int StopSessionRedirection([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
            [PreserveSig]
            int GetPackageExecutionState([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName, [Out] out PACKAGE_EXECUTION_STATE packageExecutionState);
            [PreserveSig]
            int RegisterForPackageStateChanges([In, MarshalAs(UnmanagedType.LPWStr)] string packageFullName, [In] IPackageExecutionStateChangeNotification pPackageExecutionStateChangeNotification, [Out] UIntPtr pdwCookie);
            [PreserveSig]
            int UnregisterForPackageStateChanges([In] uint dwCookie);
        }

        internal enum Uri_PROPERTY
        {
            Uri_PROPERTY_ABSOLUTE_URI = 0,
            Uri_PROPERTY_AUTHORITY = 1,
            Uri_PROPERTY_DISPLAY_URI = 2,
            Uri_PROPERTY_DOMAIN = 3,
            Uri_PROPERTY_DWORD_LAST = 0x12,
            Uri_PROPERTY_DWORD_START = 15,
            Uri_PROPERTY_EXTENSION = 4,
            Uri_PROPERTY_FRAGMENT = 5,
            Uri_PROPERTY_HOST = 6,
            Uri_PROPERTY_HOST_TYPE = 15,
            Uri_PROPERTY_PASSWORD = 7,
            Uri_PROPERTY_PATH = 8,
            Uri_PROPERTY_PATH_AND_QUERY = 9,
            Uri_PROPERTY_PORT = 0x10,
            Uri_PROPERTY_QUERY = 10,
            Uri_PROPERTY_RAW_URI = 11,
            Uri_PROPERTY_SCHEME = 0x11,
            Uri_PROPERTY_SCHEME_NAME = 12,
            Uri_PROPERTY_STRING_LAST = 14,
            Uri_PROPERTY_STRING_START = 0,
            Uri_PROPERTY_USER_INFO = 13,
            Uri_PROPERTY_USER_NAME = 14,
            Uri_PROPERTY_ZONE = 0x12
        }

        [ComImport, Guid("A39EE748-6A27-4817-A6F2-13914BEF5890"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IUri
        {
            void GetPropertyBSTR([In] Uri_PROPERTY uriProp, [MarshalAs(UnmanagedType.BStr)] out string pbstrProperty, [In] uint dwFlags);
            void GetPropertyLength([In] Uri_PROPERTY uriProp, out uint pcchProperty, [In] uint dwFlags);
            void GetPropertyDWORD([In] Uri_PROPERTY uriProp, out uint pdwProperty, [In] uint dwFlags);
            void HasProperty([In] Uri_PROPERTY uriProp, out int pfHasProperty);
            void GetAbsoluteUri([MarshalAs(UnmanagedType.BStr)] out string pbstrAbsoluteUri);
            void GetAuthority([MarshalAs(UnmanagedType.BStr)] out string pbstrAuthority);
            void GetDisplayUri([MarshalAs(UnmanagedType.BStr)] out string pbstrDisplayString);
            void GetDomain([MarshalAs(UnmanagedType.BStr)] out string pbstrDomain);
            void GetExtension([MarshalAs(UnmanagedType.BStr)] out string pbstrExtension);
            void GetFragment([MarshalAs(UnmanagedType.BStr)] out string pbstrFragment);
            void GetHost([MarshalAs(UnmanagedType.BStr)] out string pbstrHost);
            void GetPassword([MarshalAs(UnmanagedType.BStr)] out string pbstrPassword);
            void GetPath([MarshalAs(UnmanagedType.BStr)] out string pbstrPath);
            void GetPathAndQuery([MarshalAs(UnmanagedType.BStr)] out string pbstrPathAndQuery);
            void GetQuery([MarshalAs(UnmanagedType.BStr)] out string pbstrQuery);
            void GetRawUri([MarshalAs(UnmanagedType.BStr)] out string pbstrRawUri);
            void GetSchemeName([MarshalAs(UnmanagedType.BStr)] out string pbstrSchemeName);
            void GetUserInfo([MarshalAs(UnmanagedType.BStr)] out string pbstrUserInfo);
            void GetUserName([MarshalAs(UnmanagedType.BStr)] out string pbstrUserName);
            void GetHostType(out uint pdwHostType);
            void GetPort(out uint pdwPort);
            void GetScheme(out uint pdwScheme);
            void GetZone(out uint pdwZone);
            void GetProperties(out uint pdwFlags);
            void IsEqual([In, MarshalAs(UnmanagedType.Interface)] IUri pUri, out int pfEqual);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct APPX_PACKAGE_SETTINGS
        {
            public bool forceZip32;
            [MarshalAs(UnmanagedType.Interface)]
            public IUri hashMethod;
        }

        internal enum APPX_COMPRESSION_OPTION
        {
            APPX_COMPRESSION_OPTION_NONE,
            APPX_COMPRESSION_OPTION_NORMAL,
            APPX_COMPRESSION_OPTION_MAXIMUM,
            APPX_COMPRESSION_OPTION_FAST,
            APPX_COMPRESSION_OPTION_SUPERFAST
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("9099e33b-246f-41e4-881a-008eb613f858")]
        internal interface IAppxPackageWriter
        {
            void AddPayloadFile([In, MarshalAs(UnmanagedType.LPWStr)] string fileName, [In, MarshalAs(UnmanagedType.LPWStr)] string contentType,
                [In] APPX_COMPRESSION_OPTION compressionOption, [In, MarshalAs(UnmanagedType.Interface)] IStream inputStream);
            void Close([In, MarshalAs(UnmanagedType.Interface)] IStream manifest);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("beb94909-e451-438b-b5a7-d79e767b75d8")]
        internal interface IAppxFactory
        {
            uint CreatePackageWriter([In] IStream outputStream, [In] ref APPX_PACKAGE_SETTINGS settings, [Out] out IAppxPackageWriter packageWriter);
            uint CreatePackageReader([In] IStream inputStream, [Out] out IAppxPackageReader packageReader);
            uint CreateManifestReader([In] IStream inputStream, [Out] out IAppxManifestReader manifestReader);
            uint CreateBlockMapReader();
            uint CreateValidatedBlockMapReader();
        }

        [ComImport, Guid("5842a140-ff9f-4166-8f5c-62f5b7b0c781")]
        internal class AppxFactory
        {
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("91df827b-94fd-468f-827b-57f41b2f6f2e")]
        internal interface IAppxFile
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            APPX_COMPRESSION_OPTION GetCompressionOption();

            [return: MarshalAs(UnmanagedType.LPWStr)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            string GetContentType();

            [return: MarshalAs(UnmanagedType.LPWStr)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            string GetName();

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            ulong GetSize();

            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IStream GetStream();
        }
    
        internal enum APPX_FOOTPRINT_FILE_TYPE
        {
            MANIFEST = 0,
            BLOCKMAP = 1,
            SIGNATURE = 2
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("283ce2d7-7153-4a91-9649-7a0f7240945f")]
        internal interface IAppxManifestPackageId
        {
            uint GetName([Out, MarshalAs(UnmanagedType.LPWStr)] out string name);
            uint GetArchitecture();
            uint GetPublisher([Out, MarshalAs(UnmanagedType.LPWStr)] out string publisher);
            uint GetVersion([Out, MarshalAs(UnmanagedType.U8)] out ulong packageVersion);
            uint GetResourceId([Out, MarshalAs(UnmanagedType.LPWStr)] out string resourceId);
            uint ComparePublisher([In, MarshalAs(UnmanagedType.LPWStr)] string other, [Out, MarshalAs(UnmanagedType.Bool)] out bool isSame);
            uint GetPackageFullName([Out, MarshalAs(UnmanagedType.LPWStr)] out string packageFullName);
            uint GetPackageFamilyName([Out, MarshalAs(UnmanagedType.LPWStr)] out string packageFamilyName);
        };

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5da89bf4-3773-46be-b650-7e744863b7e8")]
        internal interface IAppxManifestApplication
        {
            void GetStringValue([In, MarshalAs(UnmanagedType.LPWStr)] string name, [Out, MarshalAs(UnmanagedType.LPWStr)] out string value);
            void GetAppUserModelId([Out, MarshalAs(UnmanagedType.LPWStr)] out string appUserModelId);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("9eb8a55a-f04b-4d0d-808d-686185d4847a")]
        internal interface IAppxManifestApplicationsEnumerator
        {
            uint GetCurrent([Out] out IAppxManifestApplication application);
            uint GetHasCurrent([Out] out bool hasCurrent);
            uint MoveNext([Out]  bool hasNext);
        };

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4e1bd148-55a0-4480-a3d1-15544710637c")]
        internal interface IAppxManifestReader
        {
            uint GetPackageId([Out] out IAppxManifestPackageId packageId);
            uint GetProperties();
            uint GetPackageDependencies();
            uint GetCapabilities();
            uint GetResources();
            uint GetDeviceCapabilities();
            uint GetPrerequisite();
            uint GetApplications([Out]  out IAppxManifestApplicationsEnumerator applications);
            uint GetStream([Out] out IStream manifestStream);
        };

        [ComImport,
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
        Guid("F007EEAF-9831-411C-9847-917CDC62D1FE")]
        internal interface IAppxFilesEnumerator
        {
            uint GetCurrent([Out] out IAppxFile appxFile);

            [return: MarshalAs(UnmanagedType.Bool)]
            bool GetHasCurrent();

            [return: MarshalAs(UnmanagedType.Bool)]
            bool MoveNext();
        }

        [ComImport, ComConversionLoss, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("75CF3930-3244-4FE0-A8C8-E0BCB270B889")]
        internal interface IAppxBlockMapBlock
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IntPtr GetHash(out uint bufferSize);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            uint GetCompressedSize();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6B429B5B-36EF-479E-B9EB-0C1482B49E16")]
        internal interface IAppxBlockMapBlocksEnumerator
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxBlockMapBlock GetCurrent();

            [return: MarshalAs(UnmanagedType.Bool)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            bool GetHasCurrent();

            [return: MarshalAs(UnmanagedType.Bool)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            bool MoveNext();
        }

        [ComImport, Guid("277672AC-4F63-42C1-8ABC-BEAE3600EB59"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAppxBlockMapFile
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxBlockMapBlocksEnumerator GetBlocks();

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            uint GetLocalFileHeaderSize();

            [return: MarshalAs(UnmanagedType.LPWStr)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            string GetName();

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            ulong GetUncompressedSize();

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            int ValidateFileHash([In, MarshalAs(UnmanagedType.Interface)] IStream fileStream);
        }

        [ComImport, Guid("02B856A2-4262-4070-BACB-1A8CBBC42305"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAppxBlockMapFilesEnumerator
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxBlockMapFile GetCurrent();

            [return: MarshalAs(UnmanagedType.Bool)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            bool GetHasCurrent();

            [return: MarshalAs(UnmanagedType.Bool)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            bool MoveNext();
        }

        [ComImport, Guid("5EFEC991-BCA3-42D1-9EC2-E92D609EC22A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAppxBlockMapReader
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxBlockMapFile GetFile([In, MarshalAs(UnmanagedType.LPWStr)] string fileName);

            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxBlockMapFilesEnumerator GetFiles();

            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IUri GetHashMethod();

            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IStream GetStream();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("b5c49650-99bc-481c-9a34-3d53a4106708")]
        internal interface IAppxPackageReader
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxBlockMapReader GetBlockMap();

            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxFile GetFootprintFile([In] APPX_FOOTPRINT_FILE_TYPE type);

            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxFile GetPayloadFile([In, MarshalAs(UnmanagedType.LPWStr)] string fileName);

            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxFilesEnumerator GetPayloadFiles();

            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            IAppxManifestReader GetManifest();
        }

        internal class Guids
        {
            internal static readonly Guid ApplicationActivationManager = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C");
            internal static readonly Guid AppxFactory = new Guid("5842a140-ff9f-4166-8f5c-62f5b7b0c781");
            internal static readonly Guid IApplicationActivationManager = new Guid("2e941141-7f97-4756-ba1d-9decde894a3d");
            internal static readonly Guid IAppxFactory = new Guid("beb94909-e451-438b-b5a7-d79e767b75d8");
        }
}
