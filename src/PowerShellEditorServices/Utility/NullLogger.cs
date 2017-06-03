//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Provides an implementation of ILogger that throws away all log messages,
    /// typically used when logging isn't needed.
    /// </summary>
    public class NullLogger : ILogger, IDisposable
    {
        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        /// <param name="logLevel">The level at which the message will be written.</param>
        /// <param name="logMessage">The message text to be written.</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The source file path where the calling method exists.</param>
        /// <param name="callerLineNumber">The line number of the calling method.</param>
        public void Write(
            LogLevel logLevel,
            string logMessage,
            string callerName = null,
            string callerSourceFile = null,
            int callerLineNumber = 0)
        {
        }

        /// <summary>
        /// Writes an error message and exception to the log file.
        /// </summary>
        /// <param name="errorMessage">The error message text to be written.</param>
        /// <param name="errorException">The exception to be written..</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The source file path where the calling method exists.</param>
        /// <param name="callerLineNumber">The line number of the calling method.</param>
        public void WriteException(
            string errorMessage,
            Exception errorException,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
        }

        /// <summary>
        /// Flushes any remaining log write and closes the log file.
        /// </summary>
        public void Dispose()
        {
        }
    }
}