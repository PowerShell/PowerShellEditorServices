//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Host
{
    /// <summary>
    /// Provides a simplified interface for hosting the language and debug services
    /// over the named pipe server protocol.
    /// </summary>
    public class EditorServicesHost
    {
        #region Private Fields

        private HostDetails hostDetails;
        private LanguageServer languageServer;
        private DebugAdapter debugAdapter;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the EditorServicesHost class and waits for
        /// the debugger to attach if waitForDebugger is true.
        /// </summary>
        /// <param name="hostDetails">The details of the host which is launching PowerShell Editor Services.</param>
        /// <param name="waitForDebugger">If true, causes the host to wait for the debugger to attach before proceeding.</param>
        public EditorServicesHost(HostDetails hostDetails, bool waitForDebugger)
        {
            Validate.IsNotNull(nameof(hostDetails), hostDetails);

            this.hostDetails = hostDetails;

#if DEBUG
            int waitsRemaining = 10;
            if (waitForDebugger)
            {
                while (waitsRemaining > 0 && !Debugger.IsAttached)
                {
                    Thread.Sleep(1000);
                    waitsRemaining--;
                }
            }
#endif

            // Catch unhandled exceptions for logging purposes
#if !NanoServer
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
            Logger.Initialize(logFilePath, logLevel);

            FileVersionInfo fileVersionInfo =
#if NanoServer
                FileVersionInfo.GetVersionInfo(this.GetType().GetTypeInfo().Assembly.Location);
#else
                FileVersionInfo.GetVersionInfo(this.GetType().Assembly.Location);
#endif

            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "PowerShell Editor Services Host v{0} starting (pid {1})...\r\n\r\n" +
                    "  Host application details:\r\n\r\n" +
                    "    Name: {2}\r\n    ProfileId: {3}\r\n    Version: {4}",
                    fileVersionInfo.FileVersion,
                    Process.GetCurrentProcess().Id,
                    this.hostDetails.Name,
                    this.hostDetails.ProfileId,
                    this.hostDetails.Version));
        }

        /// <summary>
        /// Starts the language service with the specified named pipe server name.
        /// </summary>
        /// <param name="languageServicePipeName">The named pipe server name for the language service.</param>
        /// <param name="profilePaths">The object containing the profile paths to load for this session.</param>
        public void StartLanguageService(string languageServicePipeName, ProfilePaths profilePaths)
        {
            this.languageServer =
                new LanguageServer(
                    hostDetails,
                    profilePaths,
                    new NamedPipeServerChannel(languageServicePipeName));

            this.languageServer.Start().Wait();

            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Language service started, listening on named pipe: {0}",
                    languageServicePipeName));
        }

        /// <summary>
        /// Starts the debug service with the specified named pipe server name.
        /// </summary>
        /// <param name="debugServicePipeName">The named pipe server name for the debug service.</param>
        public void StartDebugService(string debugServicePipeName, ProfilePaths profilePaths)
        {
            this.debugAdapter =
                new DebugAdapter(
                    hostDetails,
                    profilePaths,
                    new NamedPipeServerChannel(debugServicePipeName));

            this.debugAdapter.SessionEnded +=
                (obj, args) =>
                {
                    Logger.Write(
                        LogLevel.Normal,
                        "Previous debug session ended, restarting debug service...");

                    this.StartDebugService(debugServicePipeName, profilePaths);
                };

            this.debugAdapter.Start().Wait();

            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Debug service started, listening on named pipe: {0}",
                    debugServicePipeName));
        }

        /// <summary>
        /// Stops the language or debug services if either were started.
        /// </summary>
        public void StopServices()
        {
            this.languageServer?.Stop().Wait();
            this.languageServer = null;

            this.debugAdapter?.Stop().Wait();
            this.debugAdapter = null;
        }

        /// <summary>
        /// Waits for either the language or debug service to shut down.
        /// </summary>
        public void WaitForCompletion()
        {
            // Wait based on which server is started.  If the language server
            // hasn't been started then we may only need to wait on the debug
            // adapter to complete.
            if (this.languageServer != null)
            {
                this.languageServer.WaitForExit();
            }
            else if (this.debugAdapter != null)
            {
                this.debugAdapter.WaitForExit();
            }
        }

        #endregion

        #region Private Methods

#if !NanoServer
        static void CurrentDomain_UnhandledException(
            object sender, 
            UnhandledExceptionEventArgs e)
        {
            // Log the exception
            Logger.Write(
                LogLevel.Error,
                string.Format(
                    "FATAL UNHANDLED EXCEPTION:\r\n\r\n{0}",
                    e.ExceptionObject.ToString()));
        }
#endif

        #endregion
    }
}
