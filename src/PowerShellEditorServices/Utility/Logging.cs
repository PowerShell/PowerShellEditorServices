using System;
using System.Collections.Generic;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;
using Serilog.Sinks.Async;
using System.Runtime.CompilerServices;

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
    /// Provides logging for EditorServices
    /// </summary>
    public interface ILogger : IDisposable
    {
        /// <summary>
        /// The minimum log level that this logger instance is configured to log at.
        /// </summary>
        LogLevel MinimumConfiguredLogLevel { get; }

        /// <summary>
        /// Write a message with the given severity to the logs.
        /// </summary>
        /// <param name="logLevel">The severity level of the log message.</param>
        /// <param name="logMessage">The log message itself.</param>
        /// <param name="callerName">The name of the calling method.</param>
        /// <param name="callerSourceFile">The name of the source file of the caller.</param>
        /// <param name="callerLineNumber">The line number where the log is being called.</param>
        void Write(
            LogLevel logLevel,
            string logMessage,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0);

        /// <summary>
        /// Log an exception in the logs.
        /// </summary>
        /// <param name="errorMessage">The error message of the exception to be logged.</param>
        /// <param name="exception">The exception itself that has been thrown.</param>
        /// <param name="callerName">The name of the method in which the ILogger is being called.</param>
        /// <param name="callerSourceFile">The name of the source file in which the ILogger is being called.</param>
        /// <param name="callerLineNumber">The line number in the file where the ILogger is being called.</param>
        void WriteException(
            string errorMessage,
            Exception exception,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0);
    }


    /// <summary>
    /// Manages logging and logger constructor for EditorServices.
    /// </summary>
    public static class Logging
    {
        /// <summary>
        /// Builder class for configuring and creating logger instances.
        /// </summary>
        public class Builder
        {
            /// <summary>
            /// The level at which to log.
            /// </summary>
            private LogLevel _logLevel;

            /// <summary>
            /// Paths at which to create log files.
            /// </summary>
            private Dictionary<string, LogLevel?> _filePaths;

            /// <summary>
            /// Whether or not to send logging to the console.
            /// </summary>
            private bool _useConsole;

            /// <summary>
            /// The log level to use when logging to the console.
            /// </summary>
            private LogLevel? _consoleLogLevel;

            /// <summary>
            /// Constructs An ILogger implementation builder instance with default configurations:
            /// No log files, not logging to console, log level normal.
            /// </summary>
            public Builder()
            {
                _logLevel = Utility.LogLevel.Normal;
                _filePaths = new Dictionary<string, LogLevel?>();
                _useConsole = false;
            }

            /// <summary>
            /// The severity level of the messages to log. Not setting this makes the log level default to "Normal".
            /// </summary>
            /// <param name="logLevel">The severity level of the messages to log.</param>
            /// <returns>the ILogger builder for reuse.</returns>
            public Builder LogLevel(LogLevel logLevel)
            {
                _logLevel = logLevel;
                return this;
            }

            /// <summary>
            /// Add a path to output a log file to.
            /// </summary>
            /// <param name="filePath">The path ofethe file to log to.</param>
            /// <param name="logLevel">
            /// The minimum log level for this file, null defaults to the configured global level.
            /// Note that setting a more verbose level than the global configuration won't work --
            /// messages are filtered by the global configuration before they hit file-specific filters.
            /// </param>
            /// <returns>the ILogger builder for reuse.</returns>
            public Builder AddLogFile(string filePath, LogLevel? logLevel = null)
            {
                _filePaths.Add(filePath, logLevel);
                return this;
            }

            /// <summary>
            /// Configure the ILogger to send log messages to the console.
            /// </summary>
            /// <param name="logLevel">The minimum log level for console logging.</param>
            /// <returns>the ILogger builder for reuse.</returns>
            public Builder AddConsoleLogging(LogLevel? logLevel = null)
            {
                _useConsole = true;
                _consoleLogLevel = logLevel;
                return this;
            }

            /// <summary>
            /// Take the log configuration and use it to create An ILogger implementation.
            /// </summary>
            /// <returns>The constructed logger.</returns>
            public ILogger Build()
            {
                var configuration = new LoggerConfiguration()
                    .MinimumLevel.Is(ConvertLogLevel(_logLevel));

                if (_useConsole)
                {
                    configuration = configuration.WriteTo.Console(
                        restrictedToMinimumLevel: ConvertLogLevel(_consoleLogLevel ?? _logLevel),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Message}{NewLine}");
                }

                foreach (KeyValuePair<string, LogLevel?> logFile in _filePaths)
                {
                    configuration = configuration.WriteTo.Async(a => a.File(logFile.Key,
                        restrictedToMinimumLevel: ConvertLogLevel(logFile.Value ?? _logLevel),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Message}{NewLine}")
                    );
                }

                return new PsesLogger(configuration.CreateLogger(), _logLevel);
            }
        }

        /// <summary>
        /// A do-nothing logger that simply discards messages.
        /// </summary>
        public static ILogger NullLogger
        {
            get
            {
                return s_nullLogger ?? (s_nullLogger = CreateLogger().Build());
            }
        }

        private static ILogger s_nullLogger;

        /// <summary>
        /// Contruct An ILogger implementation with the applied configuration.
        /// </summary>
        /// <returns>The constructed logger.</returns>
        public static Builder CreateLogger()
        {
            return new Builder();
        }

        /// <summary>
        /// Convert an EditorServices log level to a Serilog log level.
        /// </summary>
        /// <param name="logLevel">The EditorServices log level.</param>
        /// <returns>The Serilog LogEventLevel corresponding to the EditorServices log level.</returns>
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

            throw new ArgumentException($"Unknown LogLevel: '{logLevel}')", nameof(logLevel));
        }
    }

    public static class ILoggerExtensions
    {
        public static ExecutionTimer LogExecutionTime(this ILogger logger, string message)
        {
            return ExecutionTimer.Start(logger, message);
        }
    }
}
