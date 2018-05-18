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

    public static class Logging
    {
        public class LoggerBuilder
        {
            private LogLevel _logLevel;
            private List<string> _filePaths;

            public LoggerBuilder()
            {
                _logLevel = Utility.LogLevel.Normal;
                _filePaths = new List<string>();
            }

            public LoggerBuilder LogLevel(LogLevel logLevel)
            {
                _logLevel = logLevel;
                return this;
            }

            public LoggerBuilder File(string filePath)
            {
                _filePaths.Add(filePath);
                return this;
            }
        }

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
                .WriteTo.File(filePath, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Message}{Newline}{Exception}")
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

        public void WriteException(string errorMessage, Exception errorException, [CallerMemberName] string callerName = null, [CallerFilePath] string callerSourceFile = null, [CallerLineNumber] int callerLineNumber = 0)
        {
            _logger.Error("{CallerSourceFile}: In '{CallerName}', line {CallerLineNumber}:\nException: {ErrorMessage}\n{ErrorException}",
                callerSourceFile, callerName, callerLineNumber, errorMessage, errorException);
        }

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
