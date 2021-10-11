﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Server;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Microsoft.PowerShell.EditorServices.Server
{
    /// <summary>
    /// Server for hosting debug sessions.
    /// </summary>
    internal class PsesDebugServer : IDisposable
    {
        /// <summary>
        /// This is a bool but must be an int, since Interlocked.Exchange can't handle a bool
        /// </summary>
        private static readonly IdempotentLatch s_psrlCtorLatch = new();

        private static readonly Lazy<CmdletInfo> s_lazyInvokeReadLineConstructorCmdletInfo = new Lazy<CmdletInfo>(() =>
        {
            var type = Type.GetType("Microsoft.PowerShell.EditorServices.Commands.InvokeReadLineConstructorCommand, Microsoft.PowerShell.EditorServices.Hosting");
            return new CmdletInfo("__Invoke-ReadLineConstructor", type);
        });

        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly bool _useTempSession;
        private readonly bool _usePSReadLine;
        private readonly TaskCompletionSource<bool> _serverStopped;

        private DebugAdapterServer _debugAdapterServer;

        private PowerShellDebugContext _debugContext;

        protected readonly ILoggerFactory _loggerFactory;

        public PsesDebugServer(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream,
            IServiceProvider serviceProvider,
            bool useTempSession,
            bool usePSReadLine)
        {
            _loggerFactory = factory;
            _inputStream = inputStream;
            _outputStream = outputStream;
            ServiceProvider = serviceProvider;
            _useTempSession = useTempSession;
            _serverStopped = new TaskCompletionSource<bool>();
            _usePSReadLine = usePSReadLine;
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
                _debugContext = ServiceProvider.GetService<PsesInternalHost>().DebugContext;
                _debugContext.IsDebugServerActive = true;

                options
                    .WithInput(_inputStream)
                    .WithOutput(_outputStream)
                    .WithServices(serviceCollection =>
                        serviceCollection
                            .AddLogging()
                            .AddOptions()
                            .AddPsesDebugServices(ServiceProvider, this, _useTempSession))
                    // TODO: Consider replacing all WithHandler with AddSingleton
                    .WithHandler<LaunchAndAttachHandler>()
                    .WithHandler<DisconnectHandler>()
                    .WithHandler<BreakpointHandlers>()
                    .WithHandler<ConfigurationDoneHandler>()
                    .WithHandler<ThreadsHandler>()
                    .WithHandler<StackTraceHandler>()
                    .WithHandler<ScopesHandler>()
                    .WithHandler<VariablesHandler>()
                    .WithHandler<DebuggerActionHandlers>()
                    .WithHandler<SourceHandler>()
                    .WithHandler<SetVariableHandler>()
                    .WithHandler<DebugEvaluateHandler>()
                    // The OnInitialize delegate gets run when we first receive the _Initialize_ request:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                    .OnInitialize(async (server, request, cancellationToken) => {
                        // Ensure the debugger mode is set correctly - this is required for remote debugging to work
                        _debugContext.EnableDebugMode();

                        var breakpointService = server.GetService<BreakpointService>();
                        // Clear any existing breakpoints before proceeding
                        await breakpointService.RemoveAllBreakpointsAsync().ConfigureAwait(false);
                    })
                    // The OnInitialized delegate gets run right before the server responds to the _Initialize_ request:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                    .OnInitialized((server, request, response, cancellationToken) => {
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
            _debugContext.IsDebugServerActive = false;
            _debugAdapterServer.Dispose();
            _inputStream.Dispose();
            _outputStream.Dispose();
            _serverStopped.SetResult(true);
        }

        public async Task WaitForShutdown()
        {
            await _serverStopped.Task.ConfigureAwait(false);
        }

        #region Events

        public event EventHandler SessionEnded;

        internal void OnSessionEnded()
        {
            SessionEnded?.Invoke(this, null);
        }

        #endregion
    }
}
