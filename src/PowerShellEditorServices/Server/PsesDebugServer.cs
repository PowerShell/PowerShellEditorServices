//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Serialization;
using OmniSharp.Extensions.JsonRpc;
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
        private static int s_hasRunPsrlStaticCtor = 0;

        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly bool _useTempSession;
        private readonly bool _usePSReadLine;
        private readonly TaskCompletionSource<bool> _serverStopped;

        private IJsonRpcServer _jsonRpcServer;
        private PowerShellContextService _powerShellContextService;

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
            _jsonRpcServer = await JsonRpcServer.From(options =>
            {
                options.Serializer = new DapProtocolSerializer();
                options.Reciever = new DapReciever();
                options.LoggerFactory = _loggerFactory;
                ILogger logger = options.LoggerFactory.CreateLogger("DebugOptionsStartup");

                // We need to let the PowerShell Context Service know that we are in a debug session
                // so that it doesn't send the powerShell/startDebugger message.
                _powerShellContextService = ServiceProvider.GetService<PowerShellContextService>();
                _powerShellContextService.IsDebugServerActive = true;

                // Needed to make sure PSReadLine's static properties are initialized in the pipeline thread.
                if (_usePSReadLine && Interlocked.Exchange(ref s_hasRunPsrlStaticCtor, 1) == 0)
                {
                    // This must be run synchronously to ensure debugging works
                    _powerShellContextService
                        .ExecuteScriptStringAsync("[System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor([Microsoft.PowerShell.PSConsoleReadLine].TypeHandle)")
                        .GetAwaiter()
                        .GetResult();
                }

                options.Services = new ServiceCollection()
                    .AddPsesDebugServices(ServiceProvider, this, _useTempSession);

                options
                    .WithInput(_inputStream)
                    .WithOutput(_outputStream);

                logger.LogInformation("Adding handlers");

                options
                    .WithHandler<InitializeHandler>()
                    .WithHandler<LaunchHandler>()
                    .WithHandler<AttachHandler>()
                    .WithHandler<DisconnectHandler>()
                    .WithHandler<SetFunctionBreakpointsHandler>()
                    .WithHandler<SetExceptionBreakpointsHandler>()
                    .WithHandler<ConfigurationDoneHandler>()
                    .WithHandler<ThreadsHandler>()
                    .WithHandler<SetBreakpointsHandler>()
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
                    .WithHandler<DebugEvaluateHandler>();

                logger.LogInformation("Handlers added");
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _powerShellContextService.IsDebugServerActive = false;
            _jsonRpcServer.Dispose();
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
