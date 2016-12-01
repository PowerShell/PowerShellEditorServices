//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Defines the possible enumeration values for the PowerShell process architecture.
    /// </summary>
    public enum PowerShellProcessArchitecture
    {
        /// <summary>
        /// The processor architecture is unknown or wasn't accessible.
        /// </summary>
        Unknown,

        /// <summary>
        /// The processor architecture is 32-bit.
        /// </summary>
        X86,

        /// <summary>
        /// The processor architecture is 64-bit.
        /// </summary>
        X64
    }

    /// <summary>
    /// Provides details about the version of the PowerShell runtime.
    /// </summary>
    public class PowerShellVersionDetails
    {
        /// <summary>
        /// Gets the version of the PowerShell runtime.
        /// </summary>
        public Version Version { get; private set; }
        
        /// <summary>
        /// Gets the full version string, either the ToString of the Version
        /// property or the GitCommitId for open-source PowerShell releases.
        /// </summary>
        public string VersionString { get; private set; }

        /// <summary>
        /// Gets the PowerShell edition (generally Desktop or Core).
        /// </summary>
        public string Edition { get; private set; }

        /// <summary>
        /// Gets the architecture of the PowerShell process.
        /// </summary>
        public PowerShellProcessArchitecture Architecture { get; private set; }

        /// <summary>
        /// Creates an instance of the PowerShellVersionDetails class.
        /// </summary>
        /// <param name="version">The version of the PowerShell runtime.</param>
        /// <param name="versionString">A string representation of the PowerShell version.</param>
        /// <param name="editionString">The string representation of the PowerShell edition.</param>
        /// <param name="architecture">The processor architecture.</param>
        public PowerShellVersionDetails(
            Version version,
            string versionString,
            string editionString,
            PowerShellProcessArchitecture architecture)
        {
            this.Version = version;
            this.VersionString = versionString;
            this.Edition = editionString;
            this.Architecture = architecture;
        }
    }
}
