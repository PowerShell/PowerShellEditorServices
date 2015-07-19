//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using System;
using System.Collections.Generic;
using System.Management.Automation.Host;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class TestConsoleHost : IConsoleHost
    {
        private Dictionary<OutputType, string> outputPerType = 
            new Dictionary<OutputType, string>();

        #region Helper Methods

        public string GetOutputForType(OutputType outputLineType)
        {
            string outputString = null;

            this.outputPerType.TryGetValue(outputLineType, out outputString);

            return outputString;
        }

        #endregion

        #region IConsoleHost Implementation

        void IConsoleHost.WriteOutput(
            string outputString,
            bool includeNewLine,
            OutputType outputType,
            ConsoleColor foregroundColor, 
            ConsoleColor backgroundColor)
        {
            string storedOutputString = null;
            if (!this.outputPerType.TryGetValue(outputType, out storedOutputString))
            {
                this.outputPerType.Add(outputType, null);
            }

            if (storedOutputString == null)
            {
                storedOutputString = outputString;
            }
            else
            {
                storedOutputString += outputString;
            }

            if (includeNewLine)
            {
                storedOutputString += Environment.NewLine;
            }

            this.outputPerType[outputType] = storedOutputString;
        }

        Task<int> IConsoleHost.PromptForChoice(
            string caption,
            string message,
            IEnumerable<ChoiceDetails> choices,
            int defaultChoice)
        {
            var taskCompletionSource = new TaskCompletionSource<int>();

            // Keep prompt options for validation

            // Run a sleep on another thread to simulate user response
            Task.Factory.StartNew(
                () =>
                {
                    // Sleep and then signal the result
                    Thread.Sleep(500);
                    taskCompletionSource.SetResult(defaultChoice);
                });

            // Return the task that will be awaited
            return taskCompletionSource.Task;
        }

        void IConsoleHost.PromptForChoiceResult(int promptId, int choiceResult)
        {
            // No need to do anything here, task has already completed.
        }

        void IConsoleHost.UpdateProgress(
            long sourceId,
            ProgressDetails progressDetails)
        {
            // TODO: Log progress
        }

        void IConsoleHost.ExitSession(int exitCode)
        {
            // TODO: Log exit code
        }

        #endregion

    }
}
