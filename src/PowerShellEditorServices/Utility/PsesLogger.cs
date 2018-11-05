using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Serilog.Core;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// An ILogger implementation object for EditorServices, acts as an adapter to Serilog.
    /// </summary>
    public class PsesLogger : ILogger
    {
        /// <summary>
        /// The name of the ERROR log level.
        /// </summary>
        private static readonly string ErrorLevelName = LogLevel.Error.ToString().ToUpper();

        /// <summary>
        /// The internal Serilog logger to log to.
        /// </summary>
        private readonly Logger _logger;

        /// <summary>
        /// Construct a new logger around a Serilog ILogger.
        /// </summary>
        /// <param name="logger">The Serilog logger to use internally.</param>
        /// <param name="minimumLogLevel">The minimum severity level the logger is configured to log messages at.</param>
        internal PsesLogger(Logger logger, LogLevel minimumLogLevel)
        {
            _logger = logger;
            MinimumConfiguredLogLevel = minimumLogLevel;
        }

        /// <summary>
        /// The minimum log level that this logger is configured to log at.
        /// </summary>
        public LogLevel MinimumConfiguredLogLevel { get; }

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

            int threadId = Thread.CurrentThread.ManagedThreadId;

            string messageTemplate = 
                "[{LogLevelName:l}] [tid:{threadId}] In method '{CallerName:l}' {CallerSourceFile:l}:{CallerLineNumber}:{IndentedLogMsg:l}";

            switch (logLevel)
            {
                case LogLevel.Diagnostic:
                    _logger.Verbose(messageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Verbose:
                    _logger.Debug(messageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Normal:
                    _logger.Information(messageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Warning:
                    _logger.Warning(messageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Error:
                    _logger.Error(messageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
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
            string indentedException = IndentMsg(exception.ToString());

            _logger.Error("[{ErrorLevelName:l}] {CallerSourceFile:l}: In method '{CallerName:l}', line {CallerLineNumber}: {ErrorMessage:l}{IndentedException:l}",
                ErrorLevelName, callerSourceFile, callerName, callerLineNumber, errorMessage, indentedException);
        }

        /// <summary>
        /// Utility function to indent a log message by one level.
        /// </summary>
        /// <param name="logMessage">The log message to indent.</param>
        /// <returns>The indented log message string.</returns>
        private static string IndentMsg(string logMessage)
        {
            return new StringBuilder(logMessage)
                .Replace(Environment.NewLine, s_indentedPrefix)
                .Insert(0, s_indentedPrefix)
                .AppendLine()
                .ToString();
        }

        /// <summary>
        /// A newline followed by a single indentation prefix.
        /// </summary>
        private static readonly string s_indentedPrefix = Environment.NewLine + "    ";

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Internal disposer.
        /// </summary>
        /// <param name="disposing">Whether or not the object is being disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.Dispose();
                }

                _disposedValue = true;
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
