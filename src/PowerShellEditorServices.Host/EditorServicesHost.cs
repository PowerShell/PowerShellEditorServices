﻿//
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

        private HostDetails hostDetails;
        private string bundledModulesPath;
        private DebugAdapter debugAdapter;
        private LanguageServer languageServer;

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
            bool waitForDebugger)
        {
            Validate.IsNotNull(nameof(hostDetails), hostDetails);

            this.hostDetails = hostDetails;
            this.bundledModulesPath = bundledModulesPath;

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

            string newLine = Environment.NewLine;

            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    $"PowerShell Editor Services Host v{fileVersionInfo.FileVersion} starting (pid {Process.GetCurrentProcess().Id})..." + newLine + newLine +
                     "  Host application details:" + newLine + newLine +
                    $"    Name:      {this.hostDetails.Name}" + newLine +
                    $"    ProfileId: {this.hostDetails.ProfileId}" + newLine +
                    $"    Version:   {this.hostDetails.Version}" + newLine +
                     "    Arch:      {0}" + newLine + newLine +
                     "  Operating system details:" + newLine + newLine +
                    $"    Version: {Environment.OSVersion.VersionString}" + newLine +
                     "    Arch:    {1}",
                    Environment.Is64BitProcess ? "64-bit" : "32-bit",
                    Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
        }

        /// <summary>
        /// Starts the language service with the specified TCP socket port.
        /// </summary>
        /// <param name="languageServicePort">The port number for the language service.</param>
        /// <param name="profilePaths">The object containing the profile paths to load for this session.</param>
        public void StartLanguageService(int languageServicePort, ProfilePaths profilePaths)
        {
            this.languageServer =
                new LanguageServer(
                    hostDetails,
                    profilePaths,
                    new TcpSocketServerChannel(languageServicePort));

            this.languageServer.Start().Wait();

            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Language service started, listening on port {0}",
                    languageServicePort));
        }

        /// <summary>
        /// Starts the debug service with the specified TCP socket port.
        /// </summary>
        /// <param name="debugServicePort">The port number for the debug service.</param>
        public void StartDebugService(int debugServicePort, ProfilePaths profilePaths)
        {
            this.debugAdapter =
                new DebugAdapter(
                    hostDetails,
                    profilePaths,
                    new TcpSocketServerChannel(debugServicePort));

            this.debugAdapter.SessionEnded +=
                (obj, args) =>
                {
                    Logger.Write(
                        LogLevel.Normal,
                        "Previous debug session ended, restarting debug service...");

                    this.StartDebugService(debugServicePort, profilePaths);
                };

            this.debugAdapter.Start().Wait();

            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Debug service started, listening on port {0}",
                    debugServicePort));
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