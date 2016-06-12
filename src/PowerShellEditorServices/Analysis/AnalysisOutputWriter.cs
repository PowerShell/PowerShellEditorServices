//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#if ScriptAnalyzer
using Microsoft.Windows.PowerShell.ScriptAnalyzer;
using System;
using System.Management.Automation;
using Microsoft.PowerShell.EditorServices.Console;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides an implementation of ScriptAnalyzer's IOutputWriter
    /// interface that writes to trace logs.
    /// </summary>

    internal class AnalysisOutputWriter : IOutputWriter
    {
        private IConsoleHost consoleHost;

        public AnalysisOutputWriter(IConsoleHost consoleHost)
        {
            this.consoleHost = consoleHost;
        }

        #region IOutputWriter Implementation

        void IOutputWriter.WriteError(ErrorRecord error)
        {
            this.consoleHost?.WriteOutput(error.ToString(), true, OutputType.Error, ConsoleColor.Red, ConsoleColor.Black);
        }

        void IOutputWriter.WriteWarning(string message)
        {
            this.consoleHost?.WriteOutput(message, true, OutputType.Warning, ConsoleColor.Yellow, ConsoleColor.Black);
        }

        void IOutputWriter.WriteVerbose(string message)
        {
        }

        void IOutputWriter.WriteDebug(string message)
        {
        }

        void IOutputWriter.ThrowTerminatingError(ErrorRecord record)
        {
            this.consoleHost?.WriteOutput(record.ToString(), true, OutputType.Error, ConsoleColor.Red, ConsoleColor.Black);
        }

        #endregion
    }
}

#endif
