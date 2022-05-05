// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Server;
using Serilog;
using Serilog.Events;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Services.Extension;

#if DEBUG
using System.Diagnostics;
using Serilog.Debugging;
#endif

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Factory class for hiding dependencies of Editor Services.
    /// </summary>
    /// <remarks>
    /// Dependency injection and logging are wrapped by factory methods on this class so that the
    /// host assembly can construct the LSP and debug servers without directly depending on <see
    /// cref="Microsoft.Extensions.Logging"/> and <see
    /// cref="Microsoft.Extensions.DependencyInjection"/>.
    /// </remarks>
    internal sealed class EditorServicesServerFactory : IDisposable
    {
        /// <summary>
        /// Create a new Editor Services factory. This method will instantiate logging.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This can only be called once because it sets global state (the logger) and that call is
        /// in <see cref="Hosting.EditorServicesRunner" />.
        /// </para>
        /// <para>
        /// TODO: Why is this a static function wrapping a constructor instead of just a
        /// constructor? In the end it returns an instance (albeit a "singleton").
        /// </para>
        /// </remarks>
        /// <param name="logPath">The path of the log file to use.</param>
        /// <param name="minimumLogLevel">The minimum log level to use.</param>
        /// <param name="hostLogger">The host logger?</param>
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

            LoggerFactory loggerFactory = new();
            loggerFactory.AddSerilog();

            // Hook up logging from the host so that its recorded in the log file
            hostLogger.Subscribe(new HostLoggerAdapter(loggerFactory));

            return new EditorServicesServerFactory(loggerFactory);
        }

        // TODO: Can we somehow refactor this member so the language and debug servers can be
        // instantiated using their constructors instead of tying them to this factory with `Create`
        // methods?
        private readonly ILoggerFactory _loggerFactory;

        private EditorServicesServerFactory(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

        /// <summary>
        /// Create the LSP server.
        /// </summary>
        /// <remarks>
        /// This is only called once and that's in <see cref="Hosting.EditorServicesRunner"/>.
        /// </remarks>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="hostStartupInfo">The host details configuration for Editor Services
        /// instantation.</param>
        /// <returns>A new, unstarted language server instance.</returns>
        public PsesLanguageServer CreateLanguageServer(
            Stream inputStream,
            Stream outputStream,
            HostStartupInfo hostStartupInfo) => new(_loggerFactory, inputStream, outputStream, hostStartupInfo);

        /// <summary>
        /// Create the debug server given a language server instance.
        /// </summary>
        /// <remarks>
        /// This is only called once and that's in <see cref="Hosting.EditorServicesRunner"/>.
        /// </remarks>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="languageServer"></param>
        /// <returns>A new, unstarted debug server instance.</returns>
        public PsesDebugServer CreateDebugServerWithLanguageServer(
            Stream inputStream,
            Stream outputStream,
            PsesLanguageServer languageServer)
        {
            return new PsesDebugServer(
                _loggerFactory,
                inputStream,
                outputStream,
                languageServer.LanguageServer.Services);
        }

        /// <summary>
        /// Create a new debug server based on an old one in an ended session.
        /// </summary>
        /// <remarks>
        /// This is only called once and that's in <see cref="Hosting.EditorServicesRunner"/>.
        /// </remarks>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="debugServer">The old debug server to recreate.</param>
        /// <returns></returns>
        public PsesDebugServer RecreateDebugServer(
            Stream inputStream,
            Stream outputStream,
            PsesDebugServer debugServer)
        {
            return new PsesDebugServer(
                _loggerFactory,
                inputStream,
                outputStream,
                debugServer.ServiceProvider);
        }

        /// <summary>
        /// Create a standalone debug server for temp sessions.
        /// </summary>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="hostStartupInfo">The host startup configuration to create the server session with.</param>
        /// <returns></returns>
        public PsesDebugServer CreateDebugServerForTempSession(
            Stream inputStream,
            Stream outputStream,
            HostStartupInfo hostStartupInfo)
        {
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder
                    .ClearProviders()
                    .AddSerilog()
                    .SetMinimumLevel(LogLevel.Trace)) // TODO: Why randomly set to trace?
                .AddSingleton<ILanguageServerFacade>(_ => null)
                .AddPsesLanguageServices(hostStartupInfo)
                // For a Temp session, there is no LanguageServer so just set it to null
                .AddSingleton(
                    typeof(ILanguageServerFacade),
                    _ => null)
                .BuildServiceProvider();

            // This gets the ExtensionService which triggers the creation of the `$psEditor` variable.
            // (because services are created only when they are first retrieved)
            // Keep in mind, for Temp sessions, the `$psEditor` API is a no-op and the user is warned
            // to run the command in the main PS Integrated Console.
            serviceProvider.GetService<ExtensionService>();

            return new PsesDebugServer(
                _loggerFactory,
                inputStream,
                outputStream,
                serviceProvider,
                isTemp: true);
        }

        /// <summary>
        /// TODO: This class probably should not be <see cref="IDisposable"/> as the primary
        /// intention of that interface is to provide cleanup of unmanaged resources, which the
        /// logger certainly is not. Nor is this class used with a <see langword="using"/>. Instead,
        /// this class should call <see cref="Log.CloseAndFlush()"/> in a finalizer. This
        /// could potentially even be done with <see
        /// cref="SerilogLoggerFactoryExtensions.AddSerilog"</> by passing <c>dispose=true</c>.
        /// </summary>
        public void Dispose()
        {
            Log.CloseAndFlush();
            _loggerFactory.Dispose();
        }
    }
}
