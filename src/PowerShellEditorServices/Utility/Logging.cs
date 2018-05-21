using System;
using System.Collections.Generic;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;
using Serilog.Sinks.Async;

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
            /// Constructs a logger builder instance with default configurations:
            /// No log files, not logging to console, log level normal.
            /// </summary>
            public Builder()
            {
                _logLevel = Utility.LogLevel.Normal;
                _filePaths = new Dictionary<string, LogLevel?>();
                _useConsole = false;
            }

            /// <summary>
            /// The severity level of the messages to log.
            /// </summary>
            /// <param name="logLevel">The severity level of the messages to log.</param>
            /// <returns>The logger builder for reuse.</returns>
            public Builder LogLevel(LogLevel logLevel)
            {
                _logLevel = logLevel;
                return this;
            }

            /// <summary>
            /// Add a path to output a log file to.
            /// </summary>
            /// <param name="filePath">The path ofethe file to log to.</param>
            /// <param name="logLevel">The minimum log level for this file</param>
            /// <param name="useMultiprocess">Set whether the log file should be readable by other processes</param>
            /// <returns>The logger builder for reuse.</returns>
            public Builder AddLogFile(string filePath, LogLevel? logLevel = null, bool useMultiprocess = false)
            {
                _filePaths.Add(filePath, logLevel);
                return this;
            }

            /// <summary>
            /// Configure the logger to send log messages to the console.
            /// </summary>
            /// <param name="logLevel">The minimum log level for console logging.</param>
            /// <returns>The logger builder for reuse.</returns>
            public Builder AddConsoleLogging(LogLevel? logLevel = null)
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
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Message}{Newline}{Exception}{Newline}");
                }

                foreach (KeyValuePair<string, LogLevel?> logFile in _filePaths)
                {
                    configuration = configuration.WriteTo.Async(a => a.File(logFile.Key,
                        restrictedToMinimumLevel: ConvertLogLevel(logFile.Value ?? _logLevel),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Message}{Newline}{Exception}{Newline}")
                    );
                }

                return new PsesLogger(configuration.CreateLogger());
            }

        }

        /// <summary>
        /// Contruct a logger with the applied configuration.
        /// </summary>
        /// <returns>The constructed logger.</returns>
        public static Builder CreateLogger()
        {
            return new Builder();
        }

        /// <summary>
        /// Create a No-Op logger, which just throws away log messages.
        /// </summary>
        /// <returns>A do-nothing logger, with no output anywhere.</returns>
        public static PsesLogger CreateNullLogger()
        {
            return CreateLogger().Build();
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

            throw new ArgumentException($"Unknown LogLevel: '{logLevel}')", nameof(logLevel));
        }
    }
}
