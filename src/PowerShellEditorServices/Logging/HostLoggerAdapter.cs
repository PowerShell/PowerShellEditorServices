using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Logging
{
    /// <summary>
    /// Adapter class to allow logging events sent by the host to be recorded by PSES' logging infrastructure.
    /// </summary>
    internal class HostLoggerAdapter : IObserver<(int logLevel, string message)>
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Create a new host logger adapter.
        /// </summary>
        /// <param name="loggerFactory">Factory to create logger instances with.</param>
        public HostLoggerAdapter(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger("HostLogs");
        }

        public void OnCompleted()
        {
            // Nothing to do; we simply don't send more log messages
        }

        public void OnError(Exception error)
        {
            _logger.LogError(error, "Error in host logger");
        }

        public void OnNext((int logLevel, string message) value)
        {
            _logger.Log((LogLevel)value.logLevel, value.message);
        }
    }
}
