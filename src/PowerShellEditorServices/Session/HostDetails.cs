//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Contains details about the current host application (most
    /// likely the editor which is using the host process).
    /// </summary>
    public class HostDetails
    {
        #region Constants

        /// <summary>
        /// The default host name for PowerShell Editor Services.  Used
        /// if no host name is specified by the host application.
        /// </summary>
        public const string DefaultHostName = "PowerShell Editor Services Host";

        /// <summary>
        /// The default host ID for PowerShell Editor Services.  Used
        /// for the host-specific profile path if no host ID is specified.
        /// </summary>
        public const string DefaultHostProfileId = "Microsoft.PowerShellEditorServices";

        /// <summary>
        /// The default host version for PowerShell Editor Services.  If
        /// no version is specified by the host application, we use 0.0.0
        /// to indicate a lack of version.
        /// </summary>
        public static readonly Version DefaultHostVersion = new Version("0.0.0");

        /// <summary>
        /// The default host details in a HostDetails object.
        /// </summary>
        public static readonly HostDetails Default = new HostDetails(null, null, null);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name of the host.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the profile ID of the host, used to determine the
        /// host-specific profile path.
        /// </summary>
        public string ProfileId { get; private set; }

        /// <summary>
        /// Gets the version of the host.
        /// </summary>
        public Version Version { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the HostDetails class.
        /// </summary>
        /// <param name="name">
        /// The display name for the host, typically in the form of
        /// "[Application Name] Host".
        /// </param>
        /// <param name="profileId">
        /// The identifier of the PowerShell host to use for its profile path.
        /// loaded. Used to resolve a profile path of the form 'X_profile.ps1'
        /// where 'X' represents the value of hostProfileId.  If null, a default
        /// will be used.
        /// </param>
        /// <param name="version">The host application's version.</param>
        public HostDetails(
            string name,
            string profileId,
            Version version)
        {
            this.Name = name ?? DefaultHostName;
            this.ProfileId = profileId ?? DefaultHostProfileId;
            this.Version = version ?? DefaultHostVersion;
        }

        #endregion
    }
}
