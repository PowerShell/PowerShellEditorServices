// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Provides utility methods for common asynchronous operations.
    /// </summary>
    internal static class AsyncUtils
    {
        /// <summary>
        /// Creates a <see cref="SemaphoreSlim" /> with an handle initial and
        /// max count of one.
        /// </summary>
        /// <returns>A simple single handle <see cref="SemaphoreSlim" />.</returns>
        internal static SemaphoreSlim CreateSimpleLockingSemaphore() => new(initialCount: 1, maxCount: 1);

        internal static Task HandleErrorsAsync(
            this Task task,
            ILogger logger,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = -1)
        {
            return task.IsCompleted && !(task.IsFaulted || task.IsCanceled)
                ? task
                : LogTaskErrors(task, logger, callerName, callerSourceFile, callerLineNumber);
        }

        private static async Task LogTaskErrors(Task task, ILogger logger, string callerName, string callerSourceFile, int callerLineNumber)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"Task canceled in '{callerName}' in file '{callerSourceFile}' line {callerLineNumber}");
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Exception thrown running task in '{callerName}' in file '{callerSourceFile}' line {callerLineNumber}");
                throw;
            }
        }
    }
}
