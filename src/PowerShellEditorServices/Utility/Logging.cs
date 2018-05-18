using System;
using System.Runtime.CompilerServices;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Defines the level indicators for log messages.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Indicates a diagnostic log message.
        /// </summary>
        Diagnostic,

        /// <summary>
        /// Indicates a verbose log message.
        /// </summary>
        Verbose,

        /// <summary>
        /// Indicates a normal, non-verbose log message.
        /// </summary>
        Normal,

        /// <summary>
        /// Indicates a warning message.
        /// </summary>
        Warning,

        /// <summary>
        /// Indicates an error message.
        /// </summary>
        Error
    }

    /// <summary>
    /// Defines an interface for writing messages to a logging implementation.
    /// </summary>
    public interface IPsesLogger
    {
        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        /// <param name="logLevel">The level at which the message will be written.</param>
        /// <param name="logMessage">The message text to be written.</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The source file path where the calling method exists.</param>
        /// <param name="callerLineNumber">The line number of the calling method.</param>
        void Write(
            LogLevel logLevel,
            string logMessage,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0);

        /// <summary>
        /// Writes an error message and exception to the log file.
        /// </summary>
        /// <param name="errorMessage">The error message text to be written.</param>
        /// <param name="errorException">The exception to be written..</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The source file path where the calling method exists.</param>
        /// <param name="callerLineNumber">The line number of the calling method.</param>
        void WriteException(
            string errorMessage,
            Exception errorException,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0);
    }

    public static class Logging
    {
        private static LogEventLevel ConvertLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Diagnostic:
                    return LogEventLevel.Verbose;

                case LogLevel.Verbose:
                    return LogEventLevel.Debug;

                case LogLevel.Normal:
                    return LogEventLevel.Information;

                case LogLevel.Warning:
                    return LogEventLevel.Warning;

                case LogLevel.Error:
                    return LogEventLevel.Error;
            }

            throw new ArgumentException(String.Format("Unknown LogLevel: '{0}'", logLevel), nameof(logLevel));
        }

        public static IPsesLogger CreateFileLogger(string filePath, LogLevel logLevel)
        {
            ILogger logger = new LoggerConfiguration()
                .MinimumLevel.Is(ConvertLogLevel(logLevel))
                .WriteTo.File(filePath)
                .CreateLogger();

            return new PsesLogger(logger);
        }
    }

    internal class PsesLogger : IPsesLogger
    {
        private readonly ILogger _logger;

        public PsesLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Write(LogLevel logLevel, string logMessage, [CallerMemberName] string callerName = null, [CallerFilePath] string callerSourceFile = null, [CallerLineNumber] int callerLineNumber = 0)
        {
        }

        public void WriteException(string errorMessage, Exception errorException, [CallerMemberName] string callerName = null, [CallerFilePath] string callerSourceFile = null, [CallerLineNumber] int callerLineNumber = 0)
        {
            throw new NotImplementedException();
        }
    }
}
