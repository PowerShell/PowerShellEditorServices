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
    /// Provides an implementation of ILogger for writing messages to
    /// a log file on disk.
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private TextWriter textWriter;
        private LogLevel minimumLogLevel = LogLevel.Verbose;

        /// <summary>
        /// Creates an ILogger implementation that writes to the specified file.
        /// </summary>
        /// <param name="logFilePath">
        /// Specifies the path at which log messages will be written.
        /// </param>
        /// <param name="minimumLogLevel">
        /// Specifies the minimum log message level to write to the log file.
        /// </param>
        public FileLogger(string logFilePath, LogLevel minimumLogLevel)
        {
            this.minimumLogLevel = minimumLogLevel;

            // Ensure that we have a usable log file path
            if (!Path.IsPathRooted(logFilePath))
            {
                logFilePath =
                    Path.Combine(
#if CoreCLR
                        AppContext.BaseDirectory,
#else
                        AppDomain.CurrentDomain.BaseDirectory,
#endif
                        logFilePath);
            }

            if (!this.TryOpenLogFile(logFilePath))
            {
                // If the log file couldn't be opened at this location,
                // try opening it in a more reliable path
                this.TryOpenLogFile(
                    Path.Combine(
#if CoreCLR
                        Environment.GetEnvironmentVariable("TEMP"),
#else
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#endif
                        Path.GetFileName(logFilePath)));
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
        public void Write(
            LogLevel logLevel,
            string logMessage,
            string callerName = null,
            string callerSourceFile = null,
            int callerLineNumber = 0)
        {
            if (this.textWriter != null &&
                logLevel >= this.minimumLogLevel)
            {
                // Print the timestamp and log level
                this.textWriter.WriteLine(
                    "{0} [{1}] - Method \"{2}\" at line {3} of {4}\r\n",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    logLevel.ToString().ToUpper(),
                    callerName,
                    callerLineNumber,
                    callerSourceFile);

                // Print out indented message lines
                foreach (var messageLine in logMessage.Split('\n'))
                {
                    this.textWriter.WriteLine("    " + messageLine.TrimEnd());
                }

                // Finish with a newline and flush the writer
                this.textWriter.WriteLine();
                this.textWriter.Flush();
            }
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
            this.Write(
                LogLevel.Error,
                $"{errorMessage}\r\n\r\n{errorException.ToString()}",
                callerName,
                callerSourceFile,
                callerLineNumber);
        }

        /// <summary>
        /// Flushes any remaining log write and closes the log file.
        /// </summary>
        public void Dispose()
        {
            if (this.textWriter != null)
            {
                this.textWriter.Flush();
                this.textWriter.Dispose();
                this.textWriter = null;
            }
        }

        private bool TryOpenLogFile(string logFilePath)
        {
            try
            {
                // Make sure the log directory exists
                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        logFilePath));

                // Open the log file for writing with UTF8 encoding
                this.textWriter =
                    new StreamWriter(
                        new FileStream(
                            logFilePath,
                            FileMode.Create),
                        Encoding.UTF8);

                return true;
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException ||
                    e is IOException)
                {
                    // This exception is thrown when we can't open the file
                    // at the path in logFilePath.  Return false to indicate
                    // that the log file couldn't be created.
                    return false;
                }

                // Unexpected exception, rethrow it
                throw;
            }
        }
    }
}