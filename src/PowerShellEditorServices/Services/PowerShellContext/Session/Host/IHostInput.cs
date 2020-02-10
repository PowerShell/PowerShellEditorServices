//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
