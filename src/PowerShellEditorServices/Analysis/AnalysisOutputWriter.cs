//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Windows.PowerShell.ScriptAnalyzer;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Analysis
{
    /// <summary>
    /// Provides an implementation of ScriptAnalyzer's IOutputWriter
    /// interface that writes to trace logs.
    /// </summary>
    internal class AnalysisOutputWriter : IOutputWriter
    {
        #region IOutputWriter Implementation

        void IOutputWriter.WriteError(ErrorRecord error)
        {
            // TODO: Find a way to trace out this output!
        }

        void IOutputWriter.WriteWarning(string message)
        {
            // TODO: Find a way to trace out this output!
        }

        void IOutputWriter.WriteVerbose(string message)
        {
            // TODO: Find a way to trace out this output!
        }

        void IOutputWriter.WriteDebug(string message)
        {
            // TODO: Find a way to trace out this output!
        }

        void IOutputWriter.ThrowTerminatingError(ErrorRecord record)
        {
            // TODO: Find a way to trace out this output!
        }

        #endregion
    }
}
