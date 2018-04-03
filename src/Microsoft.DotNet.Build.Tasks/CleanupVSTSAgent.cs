using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks
{
    public class CleanupVSTSAgent : BuildTask
    {
        public bool Clean { get; set; }	
	
        public bool Report { get; set; }

        [Required]
        public string AgentDirectory { get; set; }

        [Required]
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
    }
}
