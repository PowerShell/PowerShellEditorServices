//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Components;
using Microsoft.PowerShell.EditorServices.CodeLenses;
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

    public enum EditorServiceTransportType
    {
        NamedPipe,
        Stdio
    }

    public class EditorServiceTransportConfig
    {
        public EditorServiceTransportType TransportType { get; set; }
        /// <summary>
        /// Configures the endpoint of the transport.
        /// For Stdio it's ignored.
        /// For NamedPipe it's the pipe name.
        /// </summary>
        public string Endpoint { get; set; }
    }

    /// <summary>
    /// Provides a simplified interface for hosting the language and debug services
    /// over the named pipe server protocol.
    /// </summary>
    public class EditorServicesHost
    {
        #region Private Fields

        private string[] additionalModules;
        private string bundledModulesPath;
        private DebugAdapter debugAdapter;
        private EditorSession editorSession;
        private bool enableConsoleRepl;
        private HashSet<string> featureFlags;
        private HostDetails hostDetails;
        private LanguageServer languageServer;
        private ILogger logger;
        private ProfilePaths profilePaths;
        private TaskCompletionSource<bool> serverCompletedTask;

        private IServerListener languageServiceListener;
        private IServerListener debugServiceListener;

        #endregion

        #region Properties

        public EditorServicesHostStatus Status { get; private set; }

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
            string[] additionalModules,
            string[] featureFlags)
        {
            Validate.IsNotNull(nameof(hostDetails), hostDetails);

            this.hostDetails = hostDetails;
            this.enableConsoleRepl = enableConsoleRepl;
            this.bundledModulesPath = bundledModulesPath;
            this.additionalModules = additionalModules ?? new string[0];
            this.featureFlags = new HashSet<string>(featureFlags ?? new string[0]);
            this.serverCompletedTask = new TaskCompletionSource<bool>();

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
            this.logger = Logging.CreateLogger()
                            .LogLevel(logLevel)
                            .AddLogFile(logFilePath)
                            .Build();

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
        /// Starts the language service with the specified config.
        /// </summary>
        /// <param name="config">The config that contains information on the communication protocol that will be used.</param>
        /// <param name="profilePaths">The profiles that will be loaded in the session.</param>
        public void StartLanguageService(
            EditorServiceTransportConfig config,
            ProfilePaths profilePaths)
        {
            this.profilePaths = profilePaths;

            this.languageServiceListener = CreateServiceListener(MessageProtocolType.LanguageServer, config);

            this.languageServiceListener.ClientConnect += this.OnLanguageServiceClientConnect;
            this.languageServiceListener.Start();

            this.logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Language service started, type = {0}, endpoint = {1}",
                    config.TransportType, config.Endpoint));
        }

        private async void OnLanguageServiceClientConnect(
            object sender,
            ChannelBase serverChannel)
        {
            MessageDispatcher messageDispatcher = new MessageDispatcher(this.logger);

            ProtocolEndpoint protocolEndpoint =
                new ProtocolEndpoint(
                    serverChannel,
                    messageDispatcher,
                    this.logger);

            protocolEndpoint.UnhandledException += ProtocolEndpoint_UnhandledException;

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
                    this.serverCompletedTask,
                    this.logger);

            await this.editorSession.PowerShellContext.ImportCommandsModule(
                Path.Combine(
                    Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location),
                    @"..\..\Commands"));

            this.languageServer.Start();

            // TODO: This can be moved to the point after the $psEditor object
            // gets initialized when that is done earlier than LanguageServer.Initialize
            foreach (string module in this.additionalModules)
            {
                await this.editorSession.PowerShellContext.ExecuteCommand<System.Management.Automation.PSObject>(
                    new System.Management.Automation.PSCommand().AddCommand("Import-Module").AddArgument(module),
                    false,
                    true);
            }

            protocolEndpoint.Start();
        }

        /// <summary>
        /// Starts the debug service with the specified config.
        /// </summary>
        /// <param name="config">The config that contains information on the communication protocol that will be used.</param>
        /// <param name="profilePaths">The profiles that will be loaded in the session.</param>
        /// <param name="useExistingSession">Determines if we will reuse the session that we have.</param>
        public void StartDebugService(
            EditorServiceTransportConfig config,
            ProfilePaths profilePaths,
            bool useExistingSession)
        {
            this.debugServiceListener = CreateServiceListener(MessageProtocolType.DebugAdapter, config);
            this.debugServiceListener.ClientConnect += OnDebugServiceClientConnect;
            this.debugServiceListener.Start();

            this.logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Debug service started, type = {0}, endpoint = {1}",
                    config.TransportType, config.Endpoint));
        }

        private void OnDebugServiceClientConnect(object sender, ChannelBase serverChannel)
        {
            MessageDispatcher messageDispatcher = new MessageDispatcher(this.logger);

            ProtocolEndpoint protocolEndpoint =
                new ProtocolEndpoint(
                    serverChannel,
                    messageDispatcher,
                    this.logger);

            protocolEndpoint.UnhandledException += ProtocolEndpoint_UnhandledException;

            bool ownsEditorSession = this.editorSession == null;

            if (ownsEditorSession)
            {
                this.editorSession =
                    this.CreateDebugSession(
                        this.hostDetails,
                        profilePaths,
                        protocolEndpoint,
                        messageDispatcher,
                        this.languageServer?.EditorOperations,
                        this.enableConsoleRepl);
            }

            this.debugAdapter =
                new DebugAdapter(
                    this.editorSession,
                    ownsEditorSession,
                    messageDispatcher,
                    protocolEndpoint,
                    this.logger);

            this.debugAdapter.SessionEnded +=
                (obj, args) =>
                {
                    if (!ownsEditorSession)
                    {
                        this.logger.Write(
                            LogLevel.Normal,
                            "Previous debug session ended, restarting debug service listener...");
                        this.debugServiceListener.Stop();
                        this.debugServiceListener.Start();
                    }
                    else if (this.debugAdapter.IsUsingTempIntegratedConsole)
                    {
                        this.logger.Write(
                            LogLevel.Normal,
                            "Previous temp debug session ended");
                    }
                    else
                    {
                        // Exit the host process
                        this.serverCompletedTask.SetResult(true);
                    }
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
            PowerShellContext powerShellContext = new PowerShellContext(this.logger, this.featureFlags.Contains("PSReadLine"));

            EditorServicesPSHostUserInterface hostUserInterface =
                enableConsoleRepl
                    ? (EditorServicesPSHostUserInterface) new TerminalPSHostUserInterface(powerShellContext, this.logger)
                    : new ProtocolPSHostUserInterface(powerShellContext, messageSender, this.logger);

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

            CodeLensFeature.Create(editorSession.Components, editorSession);
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
            PowerShellContext powerShellContext = new PowerShellContext(
                this.logger,
                this.featureFlags.Contains("PSReadLine"));

            EditorServicesPSHostUserInterface hostUserInterface =
                enableConsoleRepl
                    ? (EditorServicesPSHostUserInterface) new TerminalPSHostUserInterface(powerShellContext, this.logger)
                    : new ProtocolPSHostUserInterface(powerShellContext, messageSender, this.logger);

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
                hostUserInterface,
                editorOperations);

            return editorSession;
        }

        private void ProtocolEndpoint_UnhandledException(object sender, Exception e)
        {
            this.logger.Write(
                LogLevel.Error,
                "PowerShell Editor Services is terminating due to an unhandled exception, see previous logs for details.");

            this.serverCompletedTask.SetException(e);
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
        private IServerListener CreateServiceListener(MessageProtocolType protocol, EditorServiceTransportConfig config)
        {
            switch (config.TransportType)
            {
                case EditorServiceTransportType.Stdio:
                {
                    return new StdioServerListener(protocol, this.logger);
                }

                case EditorServiceTransportType.NamedPipe:
                {
                    return new NamedPipeServerListener(protocol, config.Endpoint, this.logger);
                }

                default:
                {
                    throw new NotSupportedException();
                }
            }
        }

        #endregion
    }
}
