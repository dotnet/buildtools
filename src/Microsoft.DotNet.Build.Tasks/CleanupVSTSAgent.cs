namespace Microsoft.DotNet.Build.Tasks
{
    public class CleanupVSTSAgent : BuildTask
    {
        public override bool Execute()
        {
            Log.LogWarning($"This BuildTask has been deprecated in favor of maintenance jobs.");
            return true;
        }
    }
}
