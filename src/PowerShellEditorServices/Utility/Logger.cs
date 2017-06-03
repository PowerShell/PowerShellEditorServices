//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Provides a simple logging interface.  May be replaced with a
    /// more robust solution at a later date.
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Gets the current static ILogger instance.  This property
        /// is temporary and will be removed in an upcoming commit.
        /// </summary>
        public static ILogger CurrentLogger { get; private set; }

        /// <summary>
        /// Initializes the Logger for the current session.
        /// </summary>
        /// <param name="logger">
        /// Specifies the ILogger implementation to use for the static interface.
        /// </param>
        public static void Initialize(ILogger logger)
        {
            if (CurrentLogger != null)
            {
                CurrentLogger.Dispose();
            }

            CurrentLogger = logger;
        }

        /// <summary>
        /// Closes the Logger.
        /// </summary>
        public static void Close()
        {
            if (CurrentLogger != null)
            {
                CurrentLogger.Dispose();
            }
        }
    }
}
