// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Logging
{
    internal static class LoggerExtensions
    {
        public static void LogException(
            this ILogger logger,
            string message,
            Exception exception,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = -1) => logger.LogError(message, exception);

        public static void LogHandledException(
            this ILogger logger,
            string message,
            Exception exception,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = -1) => logger.LogWarning(message, exception);
    }
}
