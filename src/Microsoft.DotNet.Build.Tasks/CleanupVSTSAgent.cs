namespace Microsoft.DotNet.Build.Tasks
{
    public class CleanupVSTSAgent : BuildTask
    {
        public override bool Execute()
        {
            Log.LogMessage($"This BuildTask has been deprecated.");
            return false;
        }
    }
}
