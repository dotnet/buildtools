namespace Microsoft.DotNet.Build.Tasks
{
    public class CleanupVSTSAgent : BuildTask
    {
        public bool Clean { get; set; }	
	
        public bool Report { get; set; }

        public string AgentDirectory { get; set; }	
	
        public double RetentionDays { get; set; }	
	
        public int? Retries { get; set; }	
	
        public int MaximumWorkspacesToClean { get; set; } = 8;	
	
        public bool EnableLongPathRemoval { get; set; } = true;	
	
        public int? SleepTimeInMilliseconds { get; set; }	
	
        public ITaskItem[] ProcessNamesToKill { get; set; }	
	
        public string [] AdditionalCleanupDirectories { get; set; }

        public override bool Execute()
        {
            Log.LogWarning($"This BuildTask has been deprecated in favor of maintenance jobs.");
            return true;
        }

#if net45
        public struct WIN32_FIND_DATA	
        {	
            public uint dwFileAttributes;	
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;	
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;	
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;	
            public uint nFileSizeHigh;	
            public uint nFileSizeLow;	
            public uint dwReserved0;	
            public uint dwReserved1;	
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]	
            public string cFileName;	
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]	
            public string cAlternateFileName;	
        }
#endif
    }
}
