//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Session
{
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
        /// Gets the architecture of the PowerShell process, either "x86" or
        /// "x64".
        /// </summary>
        public string Architecture { get; private set; }

        /// <summary>
        /// Creates an instance of the PowerShellVersionDetails class.
        /// </summary>
        /// <param name="powerShellVersion">The version of the PowerShell runtime.</param>
        /// <param name="versionString">A string representation of the PowerShell version.</param>
        /// <param name="editionString">The string representation of the PowerShell edition.</param>
        /// <param name="architectureString">The string representation of the processor architecture.</param>
        public PowerShellVersionDetails(
            Version powerShellVersion,
            string versionString,
            string editionString,
            string architectureString)
        {
            this.Version = powerShellVersion;
            this.VersionString = versionString ?? $"{powerShellVersion.Major}.{powerShellVersion.Minor}";
            this.Edition = editionString;
            this.Architecture =
                string.Equals(architectureString, "AMD64", StringComparison.CurrentCultureIgnoreCase)
                    ? "x64"
                    : architectureString;
        }
    }
}
