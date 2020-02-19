//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides details about the progress of a particular activity.
    /// </summary>
    internal class ProgressDetails
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
