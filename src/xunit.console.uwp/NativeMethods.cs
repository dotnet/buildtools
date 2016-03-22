using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace Xunit.UwpClient
{
    internal static class NativeMethods
    {
        public const int CLSCTX_LOCAL_SERVER = 0x4;
        public const int CLSCTX_INPROC_SERVER = 0x1;

        [DllImport(@"ole32.dll")]
        public static extern int CoInitializeEx(IntPtr reserved, int coInit);

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public static extern void CoCreateInstance(
           [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
           [MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter,
           int dwClsContext,
           [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
           [MarshalAs(UnmanagedType.IUnknown)] out object rReturnedComObject);
        
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "SHCreateStreamOnFileEx", SetLastError = true)]
        public static extern void SHCreateStreamOnFileEx(string fileName, 
            STGM_CONSTANTS mode, 
            uint attributes, 
            [MarshalAs(UnmanagedType.Bool)]bool fCreate, 
            IStream stream, 
            ref IStream outStream);
    }

    [Flags]
    public enum STGM_CONSTANTS
    {
        STGM_READ = 0x0,
        STGM_WRITE = 0x1,
        STGM_READWRITE = 0x2,
        STGM_SHARE_DENY_NONE = 0x40,
        STGM_SHARE_DENY_READ = 0x30,
        STGM_SHARE_DENY_WRITE = 0x20,
        STGM_SHARE_EXCLUSIVE = 0x10,
        STGM_PRIORITY = 0x40000,
        STGM_CREATE = 0x1000,
        STGM_CONVERT = 0x20000,
        STGM_FAILIFTHERE = 0x0,
        STGM_DIRECT = 0x0,
        STGM_TRANSACTED = 0x10000,
        STGM_NOSCRATCH = 0x100000,
        STGM_NOSNAPSHOT = 0x200000,
        STGM_SIMPLE = 0x8000000,
        STGM_DIRECT_SWMR = 0x400000,
        STGM_DELETEONRELEASE = 0x4000000
    }
}
