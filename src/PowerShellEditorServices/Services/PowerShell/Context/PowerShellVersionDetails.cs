// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    using System.Management.Automation;

    /// <summary>
    /// Provides details about the version of the PowerShell runtime.
    /// </summary>
    internal class PowerShellVersionDetails
    {
        /// <summary>
        /// Gets the version of the PowerShell runtime.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Gets the PowerShell edition (generally Desktop or Core).
        /// </summary>
        public string Edition { get; }

        /// <summary>
        /// Creates an instance of the PowerShellVersionDetails class.
        /// </summary>
        /// <param name="version">The version of the PowerShell runtime.</param>
        /// <param name="editionString">The string representation of the PowerShell edition.</param>
        public PowerShellVersionDetails(
            Version version,
            string editionString)
        {
            Version = version;
            Edition = editionString;
        }

        /// <summary>
        /// Gets the PowerShell version details for the given runspace. This doesn't use
        /// VersionUtils because we may be remoting, and therefore want the remote runspace's
        /// version, not the local process.
        /// </summary>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        /// <param name="pwsh">The PowerShell instance for which to get the version.</param>
        /// <returns>A new PowerShellVersionDetails instance.</returns>
        public static PowerShellVersionDetails GetVersionDetails(ILogger logger, PowerShell pwsh)
        {
            Version powerShellVersion = new(5, 0);
            string powerShellEdition = "Desktop";

            try
            {
                Hashtable psVersionTable = pwsh
                    .AddScript("[System.Diagnostics.DebuggerHidden()]param() $PSVersionTable", useLocalScope: true)
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
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Failed to look up PowerShell version, defaulting to version 5.\r\n\r\n" + ex.ToString());
            }

            return new PowerShellVersionDetails(powerShellVersion, powerShellEdition);
        }
    }
}
