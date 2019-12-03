﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Server;
using PowerShellEditorServices.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.IO;

#if DEBUG
using Serilog.Debugging;
#endif

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Factory class for hiding dependencies of Editor Services.
    /// In particular, dependency injection and logging are wrapped by factory methods on this class
    /// so that the host assembly can construct the LSP and debug servers
    /// without taking logging or dependency injection dependencies directly.
    /// </summary>
    internal class EditorServicesServerFactory : IDisposable
    {
        /// <summary>
        /// Create a new Editor Services factory.
        /// This method will instantiate logging.
        /// </summary>
        /// <param name="logPath">The path of the log file to use.</param>
        /// <param name="minimumLogLevel">The minimum log level to use.</param>
        /// <returns></returns>
        public static EditorServicesServerFactory Create(string logPath, int minimumLogLevel, IObservable<(int logLevel, string message)> hostLogger)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Async(config => config.File(logPath))
                .MinimumLevel.Is((LogEventLevel)minimumLogLevel)
                .CreateLogger();

#if DEBUG
            SelfLog.Enable(msg => Debug.WriteLine(msg));
#endif

            ILoggerFactory loggerFactory = new LoggerFactory().AddSerilog();

            // Hook up logging from the host so that its recorded in the log file
            hostLogger.Subscribe(new HostLoggerAdapter(loggerFactory));

            return new EditorServicesServerFactory(loggerFactory, (LogLevel)minimumLogLevel);
        }

        private readonly ILoggerFactory _loggerFactory;

        private readonly Extensions.Logging.ILogger _logger;

        private readonly LogLevel _minimumLogLevel;

        private EditorServicesServerFactory(ILoggerFactory loggerFactory, LogLevel minimumLogLevel)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<EditorServicesServerFactory>();
            _minimumLogLevel = minimumLogLevel;
        }

        /// <summary>
        /// Create the LSP server.
        /// </summary>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="hostDetails">The host details configuration for Editor Services instantation.</param>
        /// <returns>A new, unstarted language server instance.</returns>
        public PsesLanguageServer CreateLanguageServer(
            Stream inputStream,
            Stream outputStream,
            HostStartupInfo hostDetails)
        {
            return new PsesLanguageServer(_loggerFactory, inputStream, outputStream, hostDetails);
        }

        /// <summary>
        /// Create the debug server given a language server instance.
        /// </summary>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="languageServer"></param>
        /// <returns>A new, unstarted debug server instance.</returns>
        public PsesDebugServer CreateDebugServerWithLanguageServer(Stream inputStream, Stream outputStream, PsesLanguageServer languageServer)
        {
            return PsesDebugServer.CreateWithLanguageServerServices(_loggerFactory, inputStream, outputStream, languageServer.LanguageServer.Services);
        }

        /// <summary>
        /// Create a new debug server based on an old one in an ended session.
        /// </summary>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="debugServer">The old debug server to recreate.</param>
        /// <returns></returns>
        public PsesDebugServer RecreateDebugServer(Stream inputStream, Stream outputStream, PsesDebugServer debugServer)
        {
            return PsesDebugServer.CreateWithLanguageServerServices(_loggerFactory, inputStream, outputStream, debugServer.ServiceProvider);
        }

        /// <summary>
        /// Create a standalone debug server for temp sessions.
        /// </summary>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="hostStartupInfo">The host startup configuration to create the server session with.</param>
        /// <returns></returns>
        public PsesDebugServer CreateDebugServerForTempSession(Stream inputStream, Stream outputStream, HostStartupInfo hostStartupInfo)
        {
            return PsesDebugServer.CreateForTempSession(_loggerFactory, inputStream, outputStream, hostStartupInfo);
        }

        public void Dispose()
        {
            Log.CloseAndFlush();
            _loggerFactory.Dispose();
        }
    }
}
