//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Wraps an existing IConsoleHost implementation so that all
    /// interface methods are dispatched through the provided
    /// SynchronizationContext.  This is primarily useful for
    /// simplifying UI applications who write their own IConsoleHost
    /// implementation.
    /// </summary>
    public class SynchronizingConsoleHostWrapper : IConsoleHost
    {
        #region Private Fields

        private IConsoleHost wrappedConsoleHost;
        private SynchronizationContext syncContext;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the SynchronizingConsoleHostWrapper
        /// class that wraps the given IConsoleHost implementation and
        /// invokes its calls through the given SynchronizationContext.
        /// </summary>
        /// <param name="wrappedConsoleHost">
        /// The IConsoleHost implementation that will be wrapped.
        /// </param>
        /// <param name="syncContext">
        /// The SynchronizationContext which will be used for invoking
        /// host operations calls on the proper thread.
        /// </param>
        public SynchronizingConsoleHostWrapper(
            IConsoleHost wrappedConsoleHost,
            SynchronizationContext syncContext)
        {
            Validate.IsNotNull("wrappedConsoleHost", wrappedConsoleHost);
            Validate.IsNotNull("syncContext", syncContext);

            this.wrappedConsoleHost = wrappedConsoleHost;
            this.syncContext = syncContext;
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
            this.syncContext.Post(
                (d) =>
                {
                    this.wrappedConsoleHost.WriteOutput(
                        outputString,
                        includeNewLine,
                        outputType,
                        foregroundColor,
                        backgroundColor);
                },
                null);
        }

        Task<int> IConsoleHost.PromptForChoice(
            string promptCaption, 
            string promptMessage, 
            IEnumerable<ChoiceDetails> choices,
            int defaultChoice)
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>();

            this.syncContext.Post(
                (d) =>
                {
                    // Now that we're on the host thread, we can invoke
                    // PromptForChoice synchronously by calling .Result
                    // on the task.
                    int choiceResult =
                        this.wrappedConsoleHost.PromptForChoice(
                            promptCaption,
                            promptMessage,
                            choices,
                            defaultChoice).Result;

                    taskCompletionSource.SetResult(choiceResult);
                },
                null);

            return taskCompletionSource.Task;
        }

        void IConsoleHost.PromptForChoiceResult(int promptId, int choiceResult)
        {
            // TODO: Need to remove this method!
            throw new NotImplementedException();
        }

        void IConsoleHost.ExitSession(int exitCode)
        {
            this.syncContext.Post(
                (d) =>
                {
                    this.wrappedConsoleHost.ExitSession(exitCode);
                },
                null);
        }

        void IConsoleHost.UpdateProgress(
            long sourceId, 
            ProgressDetails progressDetails)
        {
            this.syncContext.Post(
                (d) =>
                {
                    this.wrappedConsoleHost.UpdateProgress(
                        sourceId,
                        progressDetails);
                },
                null);
        }

        #endregion
    }
}
