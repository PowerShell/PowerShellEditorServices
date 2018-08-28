using System;
using System.Diagnostics;
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
        private readonly ILogger _logger;

        private readonly string _message;

        private readonly Stopwatch _stopwatch;

        /// <summary>
        /// Create a new execution timer and start it.
        /// </summary>
        /// <param name="logger">The logger to log the execution timer message in.</param>
        /// <param name="message">The message to prefix the execution time with.</param>
        /// <returns></returns>
        public static ExecutionTimer Start(ILogger logger, string message)
        {
            var timer = new ExecutionTimer(logger, message);
            timer._stopwatch.Start();
            return timer;
        }

        internal ExecutionTimer(ILogger logger, string message)
        {
            _logger = logger;
            _message = message;
            _stopwatch = new Stopwatch();
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

            _logger.Write(LogLevel.Verbose, logMessage);
        }
    }
}
