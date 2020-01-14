//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
            [CallerLineNumber] int callerLineNumber = -1)
        {
            logger.LogError(message, exception);
        }

        public static void LogHandledException(
            this ILogger logger,
            string message,
            Exception exception,
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerSourceFile = null,
            [CallerLineNumber] int callerLineNumber = -1)
        {
            logger.LogError(message, exception);
        }
    }
}
