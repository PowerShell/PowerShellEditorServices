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
    /// Provides a simple logging interface.  May be replaced with a
    /// more robust solution at a later date.
    /// </summary>
    public static class Logger
    {
        private static ILogger staticLogger;

        /// <summary>
        /// Initializes the Logger for the current session.
        /// </summary>
        /// <param name="logger">
        /// Specifies the ILogger implementation to use for the static interface.
        /// </param>
        /// <param name="minimumLogLevel">
        /// Optional. Specifies the minimum log message level to write to the log file.
        /// </param>
        public static void Initialize(ILogger logger)
        {
            if (staticLogger != null)
            {
                staticLogger.Dispose();
            }

            staticLogger = logger;
        }

        /// <summary>
        /// Closes the Logger.
        /// </summary>
        public static void Close()
        {
            if (staticLogger != null)
            {
                staticLogger.Dispose();
            }
        }

        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        /// <param name="logLevel">The level at which the message will be written.</param>
        /// <param name="logMessage">The message text to be written.</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The source file path where the calling method exists.</param>
        /// <param name="callerLineNumber">The line number of the calling method.</param>
        public static void Write(
            LogLevel logLevel,
            string logMessage,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            InnerWrite(
                logLevel,
                logMessage,
                callerName,
                callerSourceFile,
                callerLineNumber);
        }

        /// <summary>
        /// Writes an error message and exception to the log file.
        /// </summary>
        /// <param name="errorMessage">The error message text to be written.</param>
        /// <param name="errorException">The exception to be written..</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The source file path where the calling method exists.</param>
        /// <param name="callerLineNumber">The line number of the calling method.</param>
        public static void WriteException(
            string errorMessage,
            Exception errorException,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            InnerWrite(
                LogLevel.Error,
                $"{errorMessage}\r\n\r\n{errorException.ToString()}",
                callerName,
                callerSourceFile,
                callerLineNumber);
        }

        /// <summary>
        /// Writes an error message and exception to the log file.
        /// </summary>
        /// <param name="logLevel">The level at which the message will be written.</param>
        /// <param name="errorMessage">The error message text to be written.</param>
        /// <param name="errorException">The exception to be written..</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The source file path where the calling method exists.</param>
        /// <param name="callerLineNumber">The line number of the calling method.</param>
        public static void WriteException(
            LogLevel logLevel,
            string errorMessage,
            Exception errorException,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            InnerWrite(
                logLevel,
                $"{errorMessage}\r\n\r\n{errorException.ToString()}",
                callerName,
                callerSourceFile,
                callerLineNumber);
        }

        private static void InnerWrite(
            LogLevel logLevel,
            string logMessage,
            string callerName,
            string callerSourceFile,
            int callerLineNumber)
        {
            if (staticLogger != null)
            {
                staticLogger.Write(
                    logLevel,
                    logMessage,
                    callerName,
                    callerSourceFile,
                    callerLineNumber);
            }
        }
    }
}
