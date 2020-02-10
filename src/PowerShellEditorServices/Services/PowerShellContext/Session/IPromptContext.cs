//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides methods for interacting with implementations of ReadLine.
    /// </summary>
    internal interface IPromptContext
    {
        /// <summary>
        /// Read a string that has been input by the user.
        /// </summary>
        /// <param name="isCommandLine">Indicates if ReadLine should act like a command REPL.</param>
        /// <param name="cancellationToken">
        /// The cancellation token can be used to cancel reading user input.
        /// </param>
        /// <returns>
        /// A task object that represents the completion of reading input. The Result property will
        /// return the input string.
        /// </returns>
        Task<string> InvokeReadLineAsync(bool isCommandLine, CancellationToken cancellationToken);

        /// <summary>
        /// Performs any additional actions required to cancel the current ReadLine invocation.
        /// </summary>
        void AbortReadLine();

        /// <summary>
        /// Creates a task that completes when the current ReadLine invocation has been aborted.
        /// </summary>
        /// <returns>
        /// A task object that represents the abortion of the current ReadLine invocation.
        /// </returns>
        Task AbortReadLineAsync();

        /// <summary>
        /// Blocks until the current ReadLine invocation has exited.
        /// </summary>
        void WaitForReadLineExit();

        /// <summary>
        /// Creates a task that completes when the current ReadLine invocation has exited.
        /// </summary>
        /// <returns>
        /// A task object that represents the exit of the current ReadLine invocation.
        /// </returns>
        Task WaitForReadLineExitAsync();

        /// <summary>
        /// Adds the specified command to the history managed by the ReadLine implementation.
        /// </summary>
        /// <param name="command">The command to record.</param>
        void AddToHistory(string command);

        /// <summary>
        /// Forces the prompt handler to trigger PowerShell event handling, reliquishing control
        /// of the pipeline thread during event processing.
        /// </summary>
        void ForcePSEventHandling();
    }
}
