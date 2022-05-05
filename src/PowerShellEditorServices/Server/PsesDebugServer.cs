// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using OmniSharp.Extensions.DebugAdapter.Server;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Microsoft.PowerShell.EditorServices.Server
{
    /// <summary>
    /// Server for hosting debug sessions.
    /// </summary>
    internal class PsesDebugServer : IDisposable
    {
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly TaskCompletionSource<bool> _serverStopped;

        private DebugAdapterServer _debugAdapterServer;

        private PsesInternalHost _psesHost;

        private bool _startedPses;

        private readonly bool _isTemp;

        protected readonly ILoggerFactory _loggerFactory;

        public PsesDebugServer(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream,
            IServiceProvider serviceProvider,
            bool isTemp = false)
        {
            _loggerFactory = factory;
            _inputStream = inputStream;
            _outputStream = outputStream;
            ServiceProvider = serviceProvider;
            _isTemp = isTemp;
            _serverStopped = new TaskCompletionSource<bool>();
        }

        internal IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Start the debug server listening.
        /// </summary>
        /// <returns>A task that completes when the server is ready.</returns>
        public async Task StartAsync()
        {
            _debugAdapterServer = await DebugAdapterServer.From(options =>
            {
                // We need to let the PowerShell Context Service know that we are in a debug session
                // so that it doesn't send the powerShell/startDebugger message.
                _psesHost = ServiceProvider.GetService<PsesInternalHost>();
                _psesHost.DebugContext.IsDebugServerActive = true;

                options
                    .WithInput(_inputStream)
                    .WithOutput(_outputStream)
                    .WithServices(serviceCollection =>
                        serviceCollection
                            .AddLogging()
                            .AddOptions()
                            .AddPsesDebugServices(ServiceProvider, this))
                    // TODO: Consider replacing all WithHandler with AddSingleton
                    .WithHandler<LaunchAndAttachHandler>()
                    .WithHandler<DisconnectHandler>()
                    .WithHandler<BreakpointHandlers>()
                    .WithHandler<ConfigurationDoneHandler>()
                    .WithHandler<ThreadsHandler>()
                    .WithHandler<StackTraceHandler>()
                    .WithHandler<ScopesHandler>()
                    .WithHandler<VariablesHandler>()
                    .WithHandler<ContinueHandler>()
                    .WithHandler<NextHandler>()
                    .WithHandler<PauseHandler>()
                    .WithHandler<StepInHandler>()
                    .WithHandler<StepOutHandler>()
                    .WithHandler<SourceHandler>()
                    .WithHandler<SetVariableHandler>()
                    .WithHandler<DebugEvaluateHandler>()
                    // The OnInitialize delegate gets run when we first receive the _Initialize_ request:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                    .OnInitialize(async (server, _, _) =>
                    {
                        // We need to make sure the host has been started
                        _startedPses = !await _psesHost.TryStartAsync(new HostStartOptions(), CancellationToken.None).ConfigureAwait(false);

                        // We need to give the host a handle to the DAP so it can register
                        // notifications (specifically for sendKeyPress).
                        if (_isTemp)
                        {
                            _psesHost.DebugServer = server;
                        }

                        // Ensure the debugger mode is set correctly - this is required for remote debugging to work
                        _psesHost.DebugContext.EnableDebugMode();

                        BreakpointService breakpointService = server.GetService<BreakpointService>();
                        // Clear any existing breakpoints before proceeding
                        await breakpointService.RemoveAllBreakpointsAsync().ConfigureAwait(false);
                    })
                    // The OnInitialized delegate gets run right before the server responds to the _Initialize_ request:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                    .OnInitialized((_, _, response, _) =>
                    {
                        response.SupportsConditionalBreakpoints = true;
                        response.SupportsConfigurationDoneRequest = true;
                        response.SupportsFunctionBreakpoints = true;
                        response.SupportsHitConditionalBreakpoints = true;
                        response.SupportsLogPoints = true;
                        response.SupportsSetVariable = true;

                        return Task.CompletedTask;
                    });
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
            // Note that the lifetime of the DebugContext is longer than the debug server;
            // It represents the debugger on the PowerShell process we're in,
            // while a new debug server is spun up for every debugging session
            _psesHost.DebugContext.IsDebugServerActive = false;
            _debugAdapterServer?.Dispose();
            _inputStream.Dispose();
            _outputStream.Dispose();
            _loggerFactory.Dispose();
            _serverStopped.SetResult(true);
            // TODO: If the debugger has stopped, should we clear the breakpoints?
        }

        public async Task WaitForShutdown()
        {
            await _serverStopped.Task.ConfigureAwait(false);

            // If we started the host, we need to ensure any errors are marshalled back to us like this
            if (_startedPses)
            {
                _psesHost.TriggerShutdown();
                await _psesHost.Shutdown.ConfigureAwait(false);
            }
        }

        #region Events

        public event EventHandler SessionEnded;

        internal void OnSessionEnded() => SessionEnded?.Invoke(this, null);

        #endregion
    }
}
