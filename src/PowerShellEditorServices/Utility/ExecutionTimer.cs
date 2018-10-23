using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Simple timer to be used with `using` to time executions.
    /// </summary>
    /// <example>
    /// An example showing how ExecutionTimer is intended to be used
    /// <code>
    /// using (ExecutionTimer.Start(logger, "Execution of MyMethod completed."))
    /// {
    ///     MyMethod(various, arguments);
    /// }
    /// </code>
    /// This will print a message like "Execution of MyMethod completed. [50ms]" to the logs.
    /// </example>
    public struct ExecutionTimer : IDisposable
    {
        private static readonly ObjectPool<Stopwatch> s_stopwatchPool = new ObjectPool<Stopwatch>();

        private Stopwatch _stopwatch;

        private readonly ILogger _logger;

        private readonly string _message;

        private readonly string _callerMemberName;

        private readonly string _callerFilePath;

        private readonly int _callerLineNumber;

        /// <summary>
        /// Create a new execution timer and start it.
        /// </summary>
        /// <param name="logger">The logger to log the execution timer message in.</param>
        /// <param name="message">The message to prefix the execution time with.</param>
        /// <param name="callerMemberName">The name of the calling method or property.</param>
        /// <param name="callerFilePath">The path to the source file of the caller.</param>
        /// <param name="callerLineNumber">The line where the timer is called.</param>
        /// <returns>A new, started execution timer.</returns>
        public static ExecutionTimer Start(
            ILogger logger,
            string message,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null,
            [CallerLineNumber] int callerLineNumber = -1)
        {
            var timer = new ExecutionTimer(logger, message, callerMemberName, callerFilePath, callerLineNumber);
            timer._stopwatch.Start();
            return timer;
        }

        internal ExecutionTimer(
            ILogger logger,
            string message,
            string callerMemberName,
            string callerFilePath,
            int callerLineNumber)
        {
            _logger = logger;
            _message = message;
            _callerMemberName = callerMemberName;
            _callerFilePath = callerFilePath;
            _callerLineNumber = callerLineNumber;
            _stopwatch = s_stopwatchPool.Rent();
        }

        /// <summary>
        /// Dispose of the execution timer by stopping the stopwatch and then printing
        /// the elapsed time in the logs.
        /// </summary>
        public void Dispose()
        {
            _stopwatch.Stop();

            string logMessage = new StringBuilder()
                .Append(_message)
                .Append(" [")
                .Append(_stopwatch.ElapsedMilliseconds)
                .Append("ms]")
                .ToString();

            _stopwatch.Reset();

            s_stopwatchPool.Return(_stopwatch);

            _logger.Write(
                LogLevel.Verbose,
                logMessage,
                callerName: _callerMemberName,
                callerSourceFile: _callerFilePath,
                callerLineNumber: _callerLineNumber);
        }
    }
}
