// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Contains details about the host as well as any other information needed by Editor Services
    /// at startup time.
    /// </summary>
    /// <remarks>
    /// TODO: Simplify this as a <see langword="record"/>.
    /// </remarks>
    public sealed class HostStartupInfo
    {
        #region Constants

        /// <summary>
        /// The default host name for PowerShell Editor Services.  Used
        /// if no host name is specified by the host application.
        /// </summary>
        private const string DefaultHostName = "PowerShell Editor Services Host";

        /// <summary>
        /// The default host ID for PowerShell Editor Services.  Used
        /// for the host-specific profile path if no host ID is specified.
        /// </summary>
        private const string DefaultHostProfileId = "Microsoft.PowerShellEditorServices";

        /// <summary>
        /// The default host version for PowerShell Editor Services.  If
        /// no version is specified by the host application, we use 0.0.0
        /// to indicate a lack of version.
        /// </summary>
        private static readonly Version s_defaultHostVersion = new(0, 0, 0);

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

        public ProfilePathInfo ProfilePaths { get; }

        /// <summary>
        /// Any feature flags enabled at startup.
        /// </summary>
        public IReadOnlyList<string> FeatureFlags { get; }

        /// <summary>
        /// Names or paths of any additional modules to import on startup.
        /// </summary>
        public IReadOnlyList<string> AdditionalModules { get; }

        /// <summary>
        /// True if the integrated console is to be enabled.
        /// </summary>
        public bool ConsoleReplEnabled { get; }

        /// <summary>
        /// If true, the legacy PSES readline implementation will be used. Otherwise PSReadLine will be used.
        /// If the console REPL is not enabled, this setting will be ignored.
        /// </summary>
        public bool UsesLegacyReadLine { get; }

        /// <summary>
        /// The PowerShell host to use with Editor Services.
        /// </summary>
        public PSHost PSHost { get; }

        /// <summary>
        /// The path of the log file Editor Services should log to.
        /// </summary>
        public string LogPath { get; }

        /// <summary>
        /// The InitialSessionState will be inherited from the orginal PowerShell process. This will
        /// be used when creating runspaces so that we honor the same InitialSessionState.
        /// </summary>
        public InitialSessionState InitialSessionState { get; }

        /// <summary>
        /// The minimum log level of log events to be logged.
        /// </summary>
        /// <remarks>
        /// This is cast to all of <see cref="Hosting.PsesLogLevel"/>, <see
        /// cref="Microsoft.Extensions.Logging.LogLevel"/>, and <see
        /// cref="Serilog.Events.LogEventLevel"/>, hence it is an <c>int</c>.
        /// </remarks>
        public int LogLevel { get; }

        /// <summary>
        /// The path to find the bundled modules. User configurable for advanced usage.
        /// </summary>
        public string BundledModulePath { get; }

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
        /// <param name="psHost">The PowerShell host to use.</param>
        /// <param name="profilePaths">The set of profile paths.</param>
        /// <param name="featureFlags">Flags of features to enable.</param>
        /// <param name="additionalModules">Names or paths of additional modules to import.</param>
        /// <param name="initialSessionState">The language mode inherited from the orginal PowerShell process. This will be used when creating runspaces so that we honor the same initialSessionState including allowed modules, cmdlets and language mode.</param>
        /// <param name="logPath">The path to log to.</param>
        /// <param name="logLevel">The minimum log event level.</param>
        /// <param name="consoleReplEnabled">Enable console if true.</param>
        /// <param name="usesLegacyReadLine">Use PSReadLine if false, otherwise use the legacy readline implementation.</param>
        /// <param name="bundledModulePath">A custom path to the expected bundled modules.</param>
        public HostStartupInfo(
            string name,
            string profileId,
            Version version,
            PSHost psHost,
            ProfilePathInfo profilePaths,
            IReadOnlyList<string> featureFlags,
            IReadOnlyList<string> additionalModules,
            InitialSessionState initialSessionState,
            string logPath,
            int logLevel,
            bool consoleReplEnabled,
            bool usesLegacyReadLine,
            string bundledModulePath)
        {
            Name = name ?? DefaultHostName;
            ProfileId = profileId ?? DefaultHostProfileId;
            Version = version ?? s_defaultHostVersion;
            PSHost = psHost;
            ProfilePaths = profilePaths;
            FeatureFlags = featureFlags ?? Array.Empty<string>();
            AdditionalModules = additionalModules ?? Array.Empty<string>();
            InitialSessionState = initialSessionState;
            LogPath = logPath;
            LogLevel = logLevel;
            ConsoleReplEnabled = consoleReplEnabled;
            UsesLegacyReadLine = usesLegacyReadLine;
            BundledModulePath = bundledModulePath;
        }

        #endregion
    }

    /// <summary>
    /// This is a strange class that is generally <c>null</c> or otherwise just has a single path
    /// set. It is eventually parsed one-by-one when setting up the PowerShell runspace.
    /// </summary>
    /// <remarks>
    /// TODO: Simplify this as a <see langword="record"/>.
    /// </remarks>
    public sealed class ProfilePathInfo
    {
        public ProfilePathInfo(
            string currentUserAllHosts,
            string currentUserCurrentHost,
            string allUsersAllHosts,
            string allUsersCurrentHost)
        {
            CurrentUserAllHosts = currentUserAllHosts;
            CurrentUserCurrentHost = currentUserCurrentHost;
            AllUsersAllHosts = allUsersAllHosts;
            AllUsersCurrentHost = allUsersCurrentHost;
        }

        public string CurrentUserAllHosts { get; }

        public string CurrentUserCurrentHost { get; }

        public string AllUsersAllHosts { get; }

        public string AllUsersCurrentHost { get; }
    }
}
