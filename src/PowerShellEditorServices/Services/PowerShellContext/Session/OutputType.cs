//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Enumerates the types of output lines that will be sent
    /// to an IConsoleHost implementation.
    /// </summary>
    internal enum OutputType
    {
        /// <summary>
        /// A normal output line, usually written with the or Write-Host or
        /// Write-Output cmdlets.
        /// </summary>
        Normal,

        /// <summary>
        /// A debug output line, written with the Write-Debug cmdlet.
        /// </summary>
        Debug,

        /// <summary>
        /// A verbose output line, written with the Write-Verbose cmdlet.
        /// </summary>
        Verbose,

        /// <summary>
        /// A warning output line, written with the Write-Warning cmdlet.
        /// </summary>
        Warning,

        /// <summary>
        /// An error output line, written with the Write-Error cmdlet or
        /// as a result of some error during PowerShell pipeline execution.
        /// </summary>
        Error
    }
}
