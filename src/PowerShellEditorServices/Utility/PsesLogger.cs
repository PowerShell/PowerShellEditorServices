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
        /// The standard log template for all log entries.
        /// </summary>
        private static readonly string s_logMessageTemplate =
            "[{LogLevelName:l}] tid:{ThreadId} in '{CallerName:l}' {CallerSourceFile:l} (line {CallerLineNumber}):{IndentedLogMsg:l}";

        /// <summary>
        /// The name of the ERROR log level.
        /// </summary>
        private static readonly string ErrorLevelName = LogLevel.Error.ToString().ToUpper();

        /// <summary>
        /// The name of the WARNING log level.
        /// </summary>
        private static readonly string WarningLevelName = LogLevel.Warning.ToString().ToUpper();

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
            Write(logLevel, new StringBuilder(logMessage), callerName, callerSourceFile, callerLineNumber);
        }

        /// <summary>
        /// Write a message with the given severity to the logs. Takes a StringBuilder to allow for minimal allocation.
        /// </summary>
        /// <param name="logLevel">The severity level of the log message.</param>
        /// <param name="logMessage">The log message itself in StringBuilder form for manipulation.</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The name of the source file of the caller.</param>
        /// <param name="callerLineNumber">The line number where the log is being called.</param>
        private void Write(
            LogLevel logLevel,
            StringBuilder logMessage,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            string indentedLogMsg = IndentMsg(logMessage);
            string logLevelName = logLevel.ToString().ToUpper();

            int threadId = Thread.CurrentThread.ManagedThreadId;

            switch (logLevel)
            {
                case LogLevel.Diagnostic:
                    _logger.Verbose(s_logMessageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Verbose:
                    _logger.Debug(s_logMessageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Normal:
                    _logger.Information(s_logMessageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Warning:
                    _logger.Warning(s_logMessageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Error:
                    _logger.Error(s_logMessageTemplate, logLevelName, threadId, callerName, callerSourceFile, callerLineNumber, indentedLogMsg);
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
            StringBuilder body = FormatExceptionMessage("Exception", errorMessage, exception);
            Write(LogLevel.Error, body, callerName, callerSourceFile, callerLineNumber);
        }

        /// <summary>
        /// Log an exception that has been handled cleanly or is otherwise not expected to cause problems in the logs.
        /// </summary>
        /// <param name="errorMessage">The error message of the exception to be logged.</param>
        /// <param name="exception">The exception itself that has been thrown.</param>
        /// <param name="callerName">The name of the method in which the ILogger is being called.</param>
        /// <param name="callerSourceFile">The name of the source file in which the ILogger is being called.</param>
        /// <param name="callerLineNumber">The line number in the file where the ILogger is being called.</param>
        public void WriteHandledException(
            string errorMessage,
            Exception exception,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            StringBuilder body = FormatExceptionMessage("Handled exception", errorMessage, exception);
            Write(LogLevel.Warning, body, callerName, callerSourceFile, callerLineNumber);
        }

        /// <summary>
        /// Utility function to indent a log message by one level.
        /// </summary>
        /// <param name="logMessageBuilder">Log message string builder to transform.</param>
        /// <returns>The indented log message string.</returns>
        private static string IndentMsg(StringBuilder logMessageBuilder)
        {
            return logMessageBuilder
                .Replace(Environment.NewLine, s_indentedPrefix)
                .Insert(0, s_indentedPrefix)
                .AppendLine()
                .ToString();
        }

        /// <summary>
        /// Creates a prettified log message from an exception.
        /// </summary>
        /// <param name="messagePrelude">The user-readable tag for this exception entry.</param>
        /// <param name="errorMessage">The user-readable short description of the error.</param>
        /// <param name="exception">The exception object itself. Must not be null.</param>
        /// <returns>An indented, formatted string of the body.</returns>
        private static StringBuilder FormatExceptionMessage(
            string messagePrelude,
            string errorMessage,
            Exception exception)
        {
            var sb = new StringBuilder()
                .Append(messagePrelude).Append(": ").Append(errorMessage).Append(Environment.NewLine)
                .Append(Environment.NewLine)
                .Append(exception.ToString());

            return sb;
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
