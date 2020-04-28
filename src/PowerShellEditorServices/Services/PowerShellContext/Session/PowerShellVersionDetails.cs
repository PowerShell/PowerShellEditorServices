//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Defines the possible enumeration values for the PowerShell process architecture.
    /// </summary>
    internal enum PowerShellProcessArchitecture
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
    internal class PowerShellVersionDetails
    {
        #region Properties

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

        #endregion

        #region Constructors

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

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the PowerShell version details for the given runspace.
        /// </summary>
        /// <param name="runspace">The runspace for which version details will be gathered.</param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        /// <returns>A new PowerShellVersionDetails instance.</returns>
        public static PowerShellVersionDetails GetVersionDetails(Runspace runspace, ILogger logger)
        {
            Version powerShellVersion = new Version(5, 0);
            string versionString = null;
            string powerShellEdition = "Desktop";
            var architecture = PowerShellProcessArchitecture.Unknown;

            try
            {
                var psVersionTable = PowerShellContextService.ExecuteScriptAndGetItem<Hashtable>("$PSVersionTable", runspace, useLocalScope: true);
                if (psVersionTable != null)
                {
                    var edition = psVersionTable["PSEdition"] as string;
                    if (edition != null)
                    {
                        powerShellEdition = edition;
                    }

                    // The PSVersion value will either be of Version or SemanticVersion.
                    // In the former case, take the value directly.  In the latter case,
                    // generate a Version from its string representation.
                    var version = psVersionTable["PSVersion"];
                    if (version is Version)
                    {
                        powerShellVersion = (Version)version;
                    }
                    else if (version != null)
                    {
                        // Expected version string format is 6.0.0-alpha so build a simpler version from that
                        powerShellVersion = new Version(version.ToString().Split('-')[0]);
                    }

                    var gitCommitId = psVersionTable["GitCommitId"] as string;
                    if (gitCommitId != null)
                    {
                        versionString = gitCommitId;
                    }
                    else
                    {
                        versionString = powerShellVersion.ToString();
                    }

                    var arch = PowerShellContextService.ExecuteScriptAndGetItem<string>("$env:PROCESSOR_ARCHITECTURE", runspace, useLocalScope: true);
                    if (arch != null)
                    {
                        if (string.Equals(arch, "AMD64", StringComparison.CurrentCultureIgnoreCase))
                        {
                            architecture = PowerShellProcessArchitecture.X64;
                        }
                        else if (string.Equals(arch, "x86", StringComparison.CurrentCultureIgnoreCase))
                        {
                            architecture = PowerShellProcessArchitecture.X86;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Failed to look up PowerShell version, defaulting to version 5.\r\n\r\n" + ex.ToString());
            }

            return new PowerShellVersionDetails(
                powerShellVersion,
                versionString,
                powerShellEdition,
                architecture);
        }

        #endregion
    }
}
