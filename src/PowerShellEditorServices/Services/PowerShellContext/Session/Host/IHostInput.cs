// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides methods for integrating with the host's input system.
    /// </summary>
    internal interface IHostInput
    {
        /// <summary>
        /// Starts the host's interactive command loop.
        /// </summary>
        void StartCommandLoop();

        /// <summary>
        /// Stops the host's interactive command loop.
        /// </summary>
        void StopCommandLoop();

        /// <summary>
        /// Cancels the currently executing command or prompt.
        /// </summary>
        void SendControlC();
    }
}
