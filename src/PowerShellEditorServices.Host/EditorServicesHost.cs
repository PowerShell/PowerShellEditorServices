//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Symbols;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Host
{
    public enum EditorServicesHostStatus
    {
        Started,
        Failed,
        Ended
    }

    /// <summary>
    /// Provides a simplified interface for hosting the language and debug services
    /// over the named pipe server protocol.
    /// </summary>
    public class EditorServicesHost
    {
        #region Private Fields

        private ILogger logger;
        private bool enableConsoleRepl;
        private HostDetails hostDetails;
        private ProfilePaths profilePaths;
        private string bundledModulesPath;
        private DebugAdapter debugAdapter;
        private EditorSession editorSession;
        private HashSet<string> featureFlags;
        private LanguageServer languageServer;

        private TcpSocketServerListener languageServiceListener;
        private TcpSocketServerListener debugServiceListener;

        private TaskCompletionSource<bool> serverCompletedTask;

        #endregion

        #region Properties

        public EditorServicesHostStatus Status { get; private set; }

        public int LanguageServicePort { get; private set; }

        public int DebugServicePort { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the EditorServicesHost class and waits for
        /// the debugger to attach if waitForDebugger is true.
        /// </summary>
        /// <param name="hostDetails">The details of the host which is launching PowerShell Editor Services.</param>
        /// <param name="bundledModulesPath">Provides a path to PowerShell modules bundled with the host, if any.  Null otherwise.</param>
        /// <param name="waitForDebugger">If true, causes the host to wait for the debugger to attach before proceeding.</param>
        public EditorServicesHost(
            HostDetails hostDetails,
            string bundledModulesPath,
            bool enableConsoleRepl,
            bool waitForDebugger,
            string[] featureFlags)
        {
            Validate.IsNotNull(nameof(hostDetails), hostDetails);

            this.hostDetails = hostDetails;
            this.enableConsoleRepl = enableConsoleRepl;
            this.bundledModulesPath = bundledModulesPath;
            this.featureFlags = new HashSet<string>(featureFlags ?? new string[0]);

#if DEBUG
            if (waitForDebugger)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }
            }
#endif

            // Catch unhandled exceptions for logging purposes
#if !CoreCLR
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#endif
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the Logger for the specified file path and log level.
        /// </summary>
        /// <param name="logFilePath">The path of the log file to be written.</param>
        /// <param name="logLevel">The minimum level of log messages to be written.</param>
        public void StartLogging(string logFilePath, LogLevel logLevel)
        {
            this.logger = new FileLogger(logFilePath, logLevel);

#if CoreCLR
            FileVersionInfo fileVersionInfo =
                FileVersionInfo.GetVersionInfo(this.GetType().GetTypeInfo().Assembly.Location);

            // TODO #278: Need the correct dependency package for this to work correctly
            //string osVersionString = RuntimeInformation.OSDescription;
            //string processArchitecture = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "64-bit" : "32-bit";
            //string osArchitecture = RuntimeInformation.OSArchitecture == Architecture.X64 ? "64-bit" : "32-bit";
#else
            FileVersionInfo fileVersionInfo =
                FileVersionInfo.GetVersionInfo(this.GetType().Assembly.Location);
            string osVersionString = Environment.OSVersion.VersionString;
            string processArchitecture = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            string osArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
#endif

            string newLine = Environment.NewLine;

            this.logger.Write(
                LogLevel.Normal,
                string.Format(
                    $"PowerShell Editor Services Host v{fileVersionInfo.FileVersion} starting (pid {Process.GetCurrentProcess().Id})..." + newLine + newLine +
                     "  Host application details:" + newLine + newLine +
                    $"    Name:      {this.hostDetails.Name}" + newLine +
                    $"    ProfileId: {this.hostDetails.ProfileId}" + newLine +
                    $"    Version:   {this.hostDetails.Version}" + newLine +
#if !CoreCLR
                    $"    Arch:      {processArchitecture}" + newLine + newLine +
                     "  Operating system details:" + newLine + newLine +
                    $"    Version: {osVersionString}" + newLine +
                    $"    Arch:    {osArchitecture}"));
#else
                    ""));
#endif
        }

        /// <summary>
        /// Starts the language service with the specified TCP socket port.
        /// </summary>
        /// <param name="languageServicePort">The port number for the language service.</param>
        /// <param name="profilePaths">The object containing the profile paths to load for this session.</param>
        public void StartLanguageService(int languageServicePort, ProfilePaths profilePaths)
        {
            this.profilePaths = profilePaths;

            this.languageServiceListener =
                new TcpSocketServerListener(
                    MessageProtocolType.LanguageServer,
                    languageServicePort,
                    this.logger);

            this.languageServiceListener.ClientConnect += this.OnLanguageServiceClientConnect;
            this.languageServiceListener.Start();

            this.logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Language service started, listening on port {0}",
                    languageServicePort));
        }

        private async void OnLanguageServiceClientConnect(
            object sender,
            TcpSocketServerChannel serverChannel)
        {
            MessageDispatcher messageDispatcher = new MessageDispatcher(this.logger);

            ProtocolEndpoint protocolEndpoint =
                new ProtocolEndpoint(
                    serverChannel,
                    messageDispatcher,
                    this.logger);

            this.editorSession =
                CreateSession(
                    this.hostDetails,
                    this.profilePaths,
                    protocolEndpoint,
                    messageDispatcher,
                    this.enableConsoleRepl);

            this.languageServer =
                new LanguageServer(
                    this.editorSession,
                    messageDispatcher,
                    protocolEndpoint,
                    this.logger);

            await this.editorSession.PowerShellContext.ImportCommandsModule(
                Path.Combine(
                    Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location),
                    @"..\..\Commands"));

            this.languageServer.Start();
            protocolEndpoint.Start();
        }

        /// <summary>
        /// Starts the debug service with the specified TCP socket port.
        /// </summary>
        /// <param name="debugServicePort">The port number for the debug service.</param>
        public void StartDebugService(
            int debugServicePort,
            ProfilePaths profilePaths,
            bool useExistingSession)
        {
            this.debugServiceListener =
                new TcpSocketServerListener(
                    MessageProtocolType.DebugAdapter,
                    debugServicePort,
                    this.logger);

            this.debugServiceListener.ClientConnect += OnDebugServiceClientConnect;
            this.debugServiceListener.Start();

            this.logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Debug service started, listening on port {0}",
                    debugServicePort));
        }

        private void OnDebugServiceClientConnect(object sender, TcpSocketServerChannel serverChannel)
        {
            MessageDispatcher messageDispatcher = new MessageDispatcher(this.logger);

            ProtocolEndpoint protocolEndpoint =
                new ProtocolEndpoint(
                    serverChannel,
                    messageDispatcher,
                    this.logger);

            if (this.enableConsoleRepl)
            {
                this.debugAdapter =
                    new DebugAdapter(
                        this.editorSession,
                        false,
                        messageDispatcher,
                        protocolEndpoint,
                        this.logger);
            }
            else
            {
                EditorSession debugSession =
                    this.CreateDebugSession(
                        this.hostDetails,
                        profilePaths,
                        protocolEndpoint,
                        messageDispatcher,
                        this.languageServer?.EditorOperations,
                        false);

                this.debugAdapter =
                    new DebugAdapter(
                        debugSession,
                        true,
                        messageDispatcher,
                        protocolEndpoint,
                        this.logger);
            }

            this.debugAdapter.SessionEnded +=
                (obj, args) =>
                {
                    this.logger.Write(
                        LogLevel.Normal,
                        "Previous debug session ended, restarting debug service listener...");

                    this.debugServiceListener.Start();
                };

            this.debugAdapter.Start();
            protocolEndpoint.Start();
        }

        /// <summary>
        /// Stops the language or debug services if either were started.
        /// </summary>
        public void StopServices()
        {
            // TODO: Need a new way to shut down the services

            this.languageServer = null;

            this.debugAdapter = null;
        }

        /// <summary>
        /// Waits for either the language or debug service to shut down.
        /// </summary>
        public void WaitForCompletion()
        {
            // TODO: We need a way to know when to complete this task!
            this.serverCompletedTask = new TaskCompletionSource<bool>();
            this.serverCompletedTask.Task.Wait();
        }

        #endregion

        #region Private Methods

        private EditorSession CreateSession(
            HostDetails hostDetails,
            ProfilePaths profilePaths,
            IMessageSender messageSender,
            IMessageHandlers messageHandlers,
            bool enableConsoleRepl)
        {
            EditorSession editorSession = new EditorSession(this.logger);
            PowerShellContext powerShellContext = new PowerShellContext(this.logger);

            EditorServicesPSHostUserInterface hostUserInterface =
                enableConsoleRepl
                    ? (EditorServicesPSHostUserInterface) new TerminalPSHostUserInterface(powerShellContext, this.logger)
                    : new ProtocolPSHostUserInterface(powerShellContext, messageSender, messageHandlers, this.logger);

            EditorServicesPSHost psHost =
                new EditorServicesPSHost(
                    powerShellContext,
                    hostDetails,
                    hostUserInterface,
                    this.logger);

            Runspace initialRunspace = PowerShellContext.CreateRunspace(psHost);
            powerShellContext.Initialize(profilePaths, initialRunspace, true, hostUserInterface);

            editorSession.StartSession(powerShellContext, hostUserInterface);

            // TODO: Move component registrations elsewhere!
            editorSession.Components.Register(this.logger);
            editorSession.Components.Register(messageHandlers);
            editorSession.Components.Register(messageSender);
            editorSession.Components.Register(powerShellContext);

            DocumentSymbolFeature.Create(editorSession.Components, editorSession);

            return editorSession;
        }

        private EditorSession CreateDebugSession(
            HostDetails hostDetails,
            ProfilePaths profilePaths,
            IMessageSender messageSender,
            IMessageHandlers messageHandlers,
            IEditorOperations editorOperations,
            bool enableConsoleRepl)
        {
            EditorSession editorSession = new EditorSession(this.logger);
            PowerShellContext powerShellContext = new PowerShellContext(this.logger);

            EditorServicesPSHostUserInterface hostUserInterface =
                enableConsoleRepl
                    ? (EditorServicesPSHostUserInterface) new TerminalPSHostUserInterface(powerShellContext, this.logger)
                    : new ProtocolPSHostUserInterface(powerShellContext, messageSender, messageHandlers, this.logger);

            EditorServicesPSHost psHost =
                new EditorServicesPSHost(
                    powerShellContext,
                    hostDetails,
                    hostUserInterface,
                    this.logger);

            Runspace initialRunspace = PowerShellContext.CreateRunspace(psHost);
            powerShellContext.Initialize(profilePaths, initialRunspace, true, hostUserInterface);

            editorSession.StartDebugSession(
                powerShellContext,
                editorOperations);

            return editorSession;
        }

#if !CoreCLR
        private void CurrentDomain_UnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            // Log the exception
            this.logger.Write(
                LogLevel.Error,
                string.Format(
                    "FATAL UNHANDLED EXCEPTION:\r\n\r\n{0}",
                    e.ExceptionObject.ToString()));
        }
#endif

        #endregion
    }
}