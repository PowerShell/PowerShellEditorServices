using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides details about the progress of a particular activity.
    /// </summary>
    public class ProgressDetails
    {
        /// <summary>
        /// Gets the percentage of the activity that has been completed.
        /// </summary>
        public int PercentComplete { get; private set; }

        internal static ProgressDetails Create(ProgressRecord progressRecord)
        {
            //progressRecord.RecordType == ProgressRecordType.Completed;
            //progressRecord.Activity;
            //progressRecord.

            return new ProgressDetails
            {
                PercentComplete = progressRecord.PercentComplete
            };
        }
    }
}
