using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Manages logging and logger constructor for EditorServices.
    /// </summary>
    public static class Logging
    {
        /// <summary>
        /// Builder class for configuring and creating logger instances.
        /// </summary>
        public class LoggerBuilder
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
            /// Constructs a logger builder instance with default configurations:
            /// No log files, not logging to console, log level normal.
            /// </summary>
            public LoggerBuilder()
            {
                _logLevel = Utility.LogLevel.Normal;
                _filePaths = new Dictionary<string, LogLevel?>();
                _useConsole = false;
            }

            /// <summary>
            /// The severity level of the messages to log.
            /// </summary>
            /// <param name="logLevel">The severity level of the messages to log.</param>
            public LoggerBuilder LogLevel(LogLevel logLevel)
            {
                _logLevel = logLevel;
                return this;
            }

            /// <summary>
            /// Add a path to output a log file to.
            /// </summary>
            /// <param name="filePath">The path ofethe file to log to.</param>
            public LoggerBuilder AddLogFile(string filePath, LogLevel? logLevel = null)
            {
                _filePaths.Add(filePath, logLevel);
                return this;
            }

            /// <summary>
            /// Configure the logger to send log messages to the console.
            /// </summary>
            public LoggerBuilder AddConsoleLogging(LogLevel? logLevel = null)
            {
                _useConsole = true;
                _consoleLogLevel = logLevel;
                return this;
            }

            /// <summary>
            /// Take the log configuration use it to create a logger.
            /// </summary>
            /// <returns>The constructed logger.</returns>
            public PsesLogger Build()
            {
                var configuration = new LoggerConfiguration()
                    .MinimumLevel.Is(ConvertLogLevel(_logLevel));

                if (_useConsole)
                {
                    configuration = configuration.WriteTo.Console(
                        restrictedToMinimumLevel: ConvertLogLevel(_consoleLogLevel ?? _logLevel),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Message}{Newline}{Exception}");
                }

                foreach (KeyValuePair<string, LogLevel?> logFile in _filePaths)
                {
                    configuration = configuration.WriteTo.File(logFile.Key,
                        restrictedToMinimumLevel: ConvertLogLevel(logFile.Value ?? _logLevel),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Message}{Newline}{Exception}");
                }

                return new PsesLogger(configuration.CreateLogger());
            }

        }

        /// <summary>
        /// Contruct a logger with the applied configuration.
        /// </summary>
        /// <returns>The constructed logger.</returns>
        public static LoggerBuilder CreateLogger()
        {
            return new LoggerBuilder();
        }

        /// <summary>
        /// Convert an EditorServices log level to a Serilog log level.
        /// </summary>
        /// <param name="logLevel">The EditorServices log level.</param>
        /// <returns>The Serilog LogEventLevel corresponding to the EditorServices log level.<returns>
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
    }

    /// <summary>
    /// Logger object for EditorServices, acts as an adapter to Serilog.
    /// </summary>
    public class PsesLogger : IPsesLogger
    {
        /// <summary>
        /// The internal Serilog logger to log to.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Construct a new logger around a Serilog ILogger.
        /// </summary>
        /// <param name="logger">The Serilog logger to use internally.</param>
        public PsesLogger(ILogger logger)
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

            switch (logLevel)
            {
                case LogLevel.Diagnostic:
                    _logger.Verbose("[{LogLevel}] {CallerSourceFile}: In '{CallerName}', line {CallerLineNumber}:\n{IndentedLogMsg}",
                        logLevel, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Verbose:
                    _logger.Debug("[{LogLevel}] {CallerSourceFile}: In '{CallerName}', line {CallerLineNumber}:\n{IndentedLogMsg}",
                        logLevel, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Normal:
                    _logger.Information("[{LogLevel}] {CallerSourceFile}: In '{CallerName}', line {CallerLineNumber}:\n{IndentedLogMsg}",
                        logLevel, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Warning:
                    _logger.Warning("[{LogLevel}] {CallerSourceFile}: In '{CallerName}', line {CallerLineNumber}:\n{IndentedLogMsg}",
                        logLevel, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
                case LogLevel.Error:
                    _logger.Error("[{LogLevel}] {CallerSourceFile}: In '{CallerName}', line {CallerLineNumber}:\n{IndentedLogMsg}",
                        logLevel, callerSourceFile, callerName, callerLineNumber, indentedLogMsg);
                    return;
            }
        }

        /// <summary>
        /// Log an exception in the logs.
        /// </summary>
        /// <param name="errorMessage">The error message of the exception to be logged.</param>
        /// <param name="errorException">The exception itself that has been thrown.</param>
        /// <param name="callerName">The name of the method in which the logger is being called.</param>
        /// <param name="callerSourceFile">The name of the source file in which the logger is being called.</param>
        /// <param name="callerLineNumber">The line number in the file where the logger is being called.</param>
        public void WriteException(
            string errorMessage,
            Exception errorException,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            _logger.Error("{CallerSourceFile}: In '{CallerName}', line {CallerLineNumber}:\nException: {ErrorMessage}\n{ErrorException}",
                callerSourceFile, callerName, callerLineNumber, errorMessage, errorException);
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

            return String.Join("\n", msgLines);
        }
    }
}
