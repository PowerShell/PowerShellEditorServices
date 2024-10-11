﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Describes the desired console REPL for the Extension Terminal.
    /// </summary>
    public enum ConsoleReplKind
    {
        /// <summary>No console REPL - there will be no interactive console available.</summary>
        None = 0,
        /// <summary>Use a REPL with the legacy readline implementation. This is generally used when PSReadLine is unavailable.</summary>
        LegacyReadLine = 1,
        /// <summary>Use a REPL with the PSReadLine module for console interaction.</summary>
        PSReadLine = 2,
    }

    /// <summary>
    /// Configuration for editor services startup.
    /// </summary>
    public sealed class EditorServicesConfig
    {
        /// <summary>
        /// Create a new editor services config object,
        /// with all required fields.
        /// </summary>
        /// <param name="hostInfo">The host description object.</param>
        /// <param name="psHost">The PowerShell host to use in Editor Services.</param>
        /// <param name="sessionDetailsPath">The path to use for the session details file.</param>
        /// <param name="bundledModulePath">The path to the modules bundled with Editor Services.</param>
        /// <param name="logPath">The path to be used for Editor Services' logging.</param>
        public EditorServicesConfig(
            HostInfo hostInfo,
            PSHost psHost,
            string sessionDetailsPath,
            string bundledModulePath,
            string logPath)
        {
            HostInfo = hostInfo;
            PSHost = psHost;
            SessionDetailsPath = sessionDetailsPath;
            BundledModulePath = bundledModulePath;
            LogPath = logPath;
        }

        /// <summary>
        /// The host description object.
        /// </summary>
        public HostInfo HostInfo { get; }

        /// <summary>
        /// The PowerShell host used by Editor Services.
        /// </summary>
        public PSHost PSHost { get; }

        /// <summary>
        /// The path to use for the session details file.
        /// </summary>
        public string SessionDetailsPath { get; }

        /// <summary>
        /// The path to the modules bundled with EditorServices.
        /// </summary>
        public string BundledModulePath { get; }

        /// <summary>
        /// The path to use for logging for Editor Services.
        /// </summary>
        public string LogPath { get; }

        /// <summary>
        /// Names of or paths to any additional modules to load on startup.
        /// </summary>
        public IReadOnlyList<string> AdditionalModules { get; set; }

        /// <summary>
        /// Flags of features to enable on startup.
        /// </summary>
        public IReadOnlyList<string> FeatureFlags { get; set; }

        /// <summary>
        /// The console REPL experience to use in the Extension Terminal
        /// (including none to disable the Extension Terminal).
        /// </summary>
        public ConsoleReplKind ConsoleRepl { get; set; } = ConsoleReplKind.None;

        /// <summary>
        /// Will suppress messages to PSHost (to prevent Stdio clobbering)        
        /// </summary>
        public bool UseNullPSHostUI { get; set; }

        /// <summary>
        /// The minimum log level to log events with.
        /// </summary>
        public PsesLogLevel LogLevel { get; set; } = PsesLogLevel.Normal;

        /// <summary>
        /// Configuration for the language server protocol transport to use.
        /// </summary>
        public ITransportConfig LanguageServiceTransport { get; set; }

        /// <summary>
        /// Configuration for the debug adapter protocol transport to use.
        /// </summary>
        public ITransportConfig DebugServiceTransport { get; set; }

        /// <summary>
        /// PowerShell profile locations for Editor Services to use for its profiles.
        /// If none are provided, these will be generated from the hosting PowerShell's profile paths.
        /// </summary>
        public ProfilePathConfig ProfilePaths { get; set; }

        /// <summary>
        /// The InitialSessionState to use when creating runspaces. LanguageMode can be set here.
        /// </summary>
        public InitialSessionState InitialSessionState { get; internal set; }

        public string StartupBanner { get; set; } = @"

                  =====> PowerShell Editor Services <=====

";
    }

    /// <summary>
    /// Configuration for Editor Services' PowerShell profile paths.
    /// </summary>
    public struct ProfilePathConfig
    {
        /// <summary>
        /// The path to the profile shared by all users across all PowerShell hosts.
        /// </summary>
        public string AllUsersAllHosts { get; set; }

        /// <summary>
        /// The path to the profile shared by all users specific to this PSES host.
        /// </summary>
        public string AllUsersCurrentHost { get; set; }

        /// <summary>
        /// The path to the profile specific to the current user across all hosts.
        /// </summary>
        public string CurrentUserAllHosts { get; set; }

        /// <summary>
        /// The path to the profile specific to the current user and to this PSES host.
        /// </summary>
        public string CurrentUserCurrentHost { get; set; }
    }
}
