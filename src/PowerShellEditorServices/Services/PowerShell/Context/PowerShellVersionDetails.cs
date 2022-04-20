// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System;
using System.Collections;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    using System.Management.Automation;

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
        public Version Version { get; }

        /// <summary>
        /// Gets the full version string, either the ToString of the Version
        /// property or the GitCommitId for open-source PowerShell releases.
        /// </summary>
        public string VersionString { get; }

        /// <summary>
        /// Gets the PowerShell edition (generally Desktop or Core).
        /// </summary>
        public string Edition { get; }

        /// <summary>
        /// Gets the architecture of the PowerShell process.
        /// </summary>
        public PowerShellProcessArchitecture Architecture { get; }

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
            Version = version;
            VersionString = versionString;
            Edition = editionString;
            Architecture = architecture;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the PowerShell version details for the given runspace.
        /// </summary>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        /// <param name="pwsh">The PowerShell instance for which to to get the version.</param>
        /// <returns>A new PowerShellVersionDetails instance.</returns>
        public static PowerShellVersionDetails GetVersionDetails(ILogger logger, PowerShell pwsh)
        {
            Version powerShellVersion = new(5, 0);
            string versionString = null;
            string powerShellEdition = "Desktop";
            PowerShellProcessArchitecture architecture = PowerShellProcessArchitecture.Unknown;

            try
            {
                Hashtable psVersionTable = pwsh
                    .AddScript("$PSVersionTable", useLocalScope: true)
                    .InvokeAndClear<Hashtable>()
                    .FirstOrDefault();

                if (psVersionTable != null)
                {
                    if (psVersionTable["PSEdition"] is string edition)
                    {
                        powerShellEdition = edition;
                    }

                    // The PSVersion value will either be of Version or SemanticVersion.
                    // In the former case, take the value directly.  In the latter case,
                    // generate a Version from its string representation.
                    object version = psVersionTable["PSVersion"];
                    if (version is Version version2)
                    {
                        powerShellVersion = version2;
                    }
                    else if (version != null)
                    {
                        // Expected version string format is 6.0.0-alpha so build a simpler version from that
                        powerShellVersion = new Version(version.ToString().Split('-')[0]);
                    }

                    versionString = psVersionTable["GitCommitId"] is string gitCommitId ? gitCommitId : powerShellVersion.ToString();

                    PSCommand procArchCommand = new PSCommand().AddScript("$env:PROCESSOR_ARCHITECTURE", useLocalScope: true);

                    string arch = pwsh
                        .AddScript("$env:PROCESSOR_ARCHITECTURE", useLocalScope: true)
                        .InvokeAndClear<string>()
                        .FirstOrDefault();

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
