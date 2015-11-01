//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class TestConsoleHost 
    {
        #region IConsoleHost Implementation

        Task<int> PromptForChoice(
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

        void PromptForChoiceResult(int promptId, int choiceResult)
        {
            // No need to do anything here, task has already completed.
        }

        void UpdateProgress(
            long sourceId,
            ProgressDetails progressDetails)
        {
            // TODO: Log progress
        }

        void ExitSession(int exitCode)
        {
            // TODO: Log exit code
        }

        #endregion
    }
}
