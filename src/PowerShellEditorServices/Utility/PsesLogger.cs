using System;
using System.Runtime.CompilerServices;
using Serilog.Core;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// An ILogger implementation object for EditorServices, acts as an adapter to Serilog.
    /// </summary>
    public class PsesLogger : ILogger
    {
        /// <summary>
        /// The internal Serilog logger to log to.
        /// </summary>
        private readonly Logger _logger;

        /// <summary>
        /// Construct a new logger around a Serilog ILogger.
        /// </summary>
        /// <param name="logger">The Serilog logger to use internally.</param>
        internal PsesLogger(Logger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Write a message with the given severity to the logs.
        /// </summary>
        /// <param name="logLevel">The severity level of the log message.</param>
        /// <param name="logMessage">The log message itself.</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The name of the source file of the caller.</param>
        /// <param name="callerLineNumber">The line number where the log is being called.</param>
        public void Write(
            LogLevel logLevel,
            string logMessage,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            string indentedLogMsg = IndentMsg(logMessage);
            string logLevelName = logLevel.ToString().ToUpper();

            switch (logLevel)
            {
                case LogLevel.Diagnostic:
                    _logger.Verbose("[{LogLevelName:l}] {CallerSourceFile:l}: In '{CallerName:l}', line {CallerLineNumber}:\n{IndentedLogMsg:l}",
                        logLevelName, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Verbose:
                    _logger.Debug("[{LogLevelName:l}] {CallerSourceFile:l}: In '{CallerName:l}', line {CallerLineNumber}:\n{IndentedLogMsg:l}",
                        logLevelName, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Normal:
                    _logger.Information("[{LogLevelName:l}] {CallerSourceFile:l}: In '{CallerName:l}', line {CallerLineNumber}:\n{IndentedLogMsg:l}",
                        logLevelName, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Warning:
                    _logger.Warning("[{LogLevelName:l}] {CallerSourceFile:l}: In '{CallerName:l}', line {CallerLineNumber}:\n{IndentedLogMsg:l}",
                        logLevelName, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Error:
                    _logger.Error("[{LogLevelName:l}] {CallerSourceFile:l}: In '{CallerName:l}', line {CallerLineNumber}:\n{IndentedLogMsg:l}",
                        logLevelName, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
            }
        }

        /// <summary>
        /// Log an exception in the logs.
        /// </summary>
        /// <param name="errorMessage">The error message of the exception to be logged.</param>
        /// <param name="exception">The exception itself that has been thrown.</param>
        /// <param name="callerName">The name of the method in which the ILogger is being called.</param>
        /// <param name="callerSourceFile">The name of the source file in which the ILogger is being called.</param>
        /// <param name="callerLineNumber">The line number in the file where the ILogger is being called.</param>
        public void WriteException(
            string errorMessage,
            Exception exception,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            _logger.Error("[{Error:l}] {CallerSourceFile:l}: In '{CallerName:l}', line {CallerLineNumber}:\n    {ErrorMessage:l}\n    {Exception:l}\n",
                LogLevel.Error.ToString().ToUpper(), callerSourceFile, callerName, callerLineNumber, errorMessage, exception);
        }

        /// <summary>
        /// Utility function to indent a log message by one level.
        /// </summary>
        /// <param name="logMessage">The log message to indent.</param>
        /// <returns>The indented log message string.</returns>
        private static string IndentMsg(string logMessage)
        {
            string[] msgLines = logMessage.Split('\n');

            for (int i = 0; i < msgLines.Length; i++)
            {
                msgLines[i] = msgLines[i].Insert(0, "    ");
            }

            return String.Join("\n", msgLines)+"\n";
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Internal disposer.
        /// </summary>
        /// <param name="disposing">Whether or not the object is being disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _logger.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Dispose of this object, using the Dispose pattern.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
