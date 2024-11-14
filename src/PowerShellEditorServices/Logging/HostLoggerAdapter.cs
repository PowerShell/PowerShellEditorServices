// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.PowerShell.EditorServices.Logging
{
    /// <summary>
    /// Adapter class to allow logging events sent by the host to be recorded by PSES' logging infrastructure.
    /// </summary>
    internal class HostLoggerAdapter(ILogger logger) : IObserver<(int logLevel, string message)>
    {
        public void OnError(Exception error) => logger.LogError(error, "Error in host logger");

        public void OnNext((int logLevel, string message) value) => logger.Log((LogLevel)value.logLevel, value.message);

        public void OnCompleted()
        {
            // Nothing to do; we simply don't send more log messages
        }

    }
}
