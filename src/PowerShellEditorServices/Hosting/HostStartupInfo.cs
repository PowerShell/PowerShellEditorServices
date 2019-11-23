using System;
using System.Collections.Generic;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Contains details about the current host application (most
    /// likely the editor which is using the host process).
    /// </summary>
    public class HostStartupInfo
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

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name of the host.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the profile ID of the host, used to determine the
        /// host-specific profile path.
        /// </summary>
        public string ProfileId { get; }

        /// <summary>
        /// Gets the version of the host.
        /// </summary>
        public Version Version { get; }

        public string CurrentUserProfilePath { get; }

        public string AllUsersProfilePath { get; }

        public IReadOnlyList<string> FeatureFlags { get; }

        public IReadOnlyList<string> AdditionalModules { get; }

        public bool ConsoleReplEnabled { get; }

        public bool UsesLegacyReadLine { get; }

        public PSHost PSHost { get; }

        public string LogPath { get; }

        public int LogLevel { get; }

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
        public HostStartupInfo(
            string name,
            string profileId,
            Version version,
            PSHost psHost,
            string allUsersProfilePath,
            string currentUsersProfilePath,
            IReadOnlyList<string> featureFlags,
            IReadOnlyList<string> additionalModules,
            string logPath,
            int logLevel,
            bool consoleReplEnabled,
            bool usesLegacyReadLine)
        {
            Name = name ?? DefaultHostName;
            ProfileId = profileId ?? DefaultHostProfileId;
            Version = version ?? DefaultHostVersion;
            PSHost = psHost;
            AllUsersProfilePath = allUsersProfilePath;
            CurrentUserProfilePath = currentUsersProfilePath;
            FeatureFlags = featureFlags ?? Array.Empty<string>();
            AdditionalModules = additionalModules ?? Array.Empty<string>();
            LogPath = logPath;
            LogLevel = logLevel;
            ConsoleReplEnabled = consoleReplEnabled;
            UsesLegacyReadLine = usesLegacyReadLine;
        }

        #endregion
    }
}
