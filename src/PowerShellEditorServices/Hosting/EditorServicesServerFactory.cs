// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Services.Extension;

// The HostLogger type isn't directly referenced from this assembly, however it uses a common IObservable interface and this alias helps make it more clear the purpose. We can use Microsoft.Extensions.Logging from this point because the ALC should be loaded, but we need to only expose the IObservable to the Hosting assembly so it doesn't try to load MEL before the ALC is ready.
using HostLogger = System.IObservable<(int logLevel, string message)>;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Factory for creating the LSP server and debug server instances.
    /// </summary>
    internal sealed class EditorServicesServerFactory : IDisposable
    {
        private readonly HostLogger _hostLogger;

        /// <summary>
        /// Creates a loggerfactory for this instance
        /// </summary>
        /// <param name="hostLogger">The hostLogger that will be provided to the language services for logging handoff</param>
        internal EditorServicesServerFactory(HostLogger hostLogger) => _hostLogger = hostLogger;

        /// <summary>
        /// Create the LSP server.
        /// </summary>
        /// <remarks>
        /// This is only called once and that's in <see cref="Hosting.EditorServicesRunner"/>.
        /// </remarks>
        /// <param name="inputStream">The protocol transport input stream.</param>
        /// <param name="outputStream">The protocol transport output stream.</param>
        /// <param name="hostStartupInfo">The host details configuration for Editor Services
        /// instantiation.</param>
        /// <returns>A new, unstarted language server instance.</returns>
        public PsesLanguageServer CreateLanguageServer(
            Stream inputStream,
            Stream outputStream,
            HostStartupInfo hostStartupInfo) => new(_hostLogger, inputStream, outputStream, hostStartupInfo);

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
                _hostLogger,
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
                _hostLogger,
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
                    .SetMinimumLevel(LogLevel.Trace)) // TODO: Why randomly set to trace?
                .AddSingleton<ILanguageServerFacade>(_ => null)
                // TODO: Why add these for a debug server?!
                .AddPsesLanguageServices(hostStartupInfo)
                // For a Temp session, there is no LanguageServer so just set it to null
                .AddSingleton(
                    typeof(ILanguageServerFacade),
                    _ => null)
                .BuildServiceProvider();

            // This gets the ExtensionService which triggers the creation of the `$psEditor` variable.
            // (because services are created only when they are first retrieved)
            // Keep in mind, for Temp sessions, the `$psEditor` API is a no-op and the user is warned
            // to run the command in the main extension terminal.
            serviceProvider.GetService<ExtensionService>();

            return new PsesDebugServer(
                _hostLogger,
                inputStream,
                outputStream,
                serviceProvider,
                isTemp: true);
        }

        // TODO: Clean up host logger? Shouldn't matter since we start a new process after shutdown.
        public void Dispose() { }
    }
}
