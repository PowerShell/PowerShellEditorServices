//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Host;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Serialization;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace Microsoft.PowerShell.EditorServices.Server
{
    public class PsesDebugServer : IDisposable
    {
        protected readonly ILoggerFactory _loggerFactory;
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly bool _useTempSession;

        private IJsonRpcServer _jsonRpcServer;

        private PowerShellContextService _powerShellContextService;

        private readonly TaskCompletionSource<bool> _serverStopped;

        public static PsesDebugServer CreateWithLanguageServerServices(
            ILoggerFactory loggerFactory,
            Stream inputStream,
            Stream outputStream,
            IServiceProvider languageServerServiceProvider)
        {
            return new PsesDebugServer(loggerFactory, inputStream, outputStream, languageServerServiceProvider, useTempSession: false);
        }

        public static PsesDebugServer CreateForTempSession(
            ILoggerFactory loggerFactory,
            LogLevel minimumLogLevel,
            Stream inputStream,
            Stream outputStream,
            IReadOnlyCollection<string> featureFlags,
            HostDetails hostDetails,
            IReadOnlyList<string> additionalModules)
        {
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder
                    .ClearProviders()
                    .AddSerilog()
                    .SetMinimumLevel(LogLevel.Trace))
                .AddSingleton<ILanguageServer>(provider => null)
                .AddPsesLanguageServices(
                    new HashSet<string>(featureFlags, StringComparer.OrdinalIgnoreCase),
                    hostDetails,
                    additionalModules)
                .BuildServiceProvider();

            return new PsesDebugServer(loggerFactory, inputStream, outputStream, serviceProvider, useTempSession: true);
        }

        private PsesDebugServer(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream,
            IServiceProvider serviceProvider,
            bool useTempSession)
        {
            _loggerFactory = factory;
            _inputStream = inputStream;
            _outputStream = outputStream;
            ServiceProvider = serviceProvider;
            _useTempSession = useTempSession;
            _serverStopped = new TaskCompletionSource<bool>();
        }

        internal IServiceProvider ServiceProvider { get; }

        public async Task StartAsync()
        {
            _jsonRpcServer = await JsonRpcServer.From(options =>
            {
                options.Serializer = new DapProtocolSerializer();
                options.Reciever = new DapReciever();
                options.LoggerFactory = _loggerFactory;
                Extensions.Logging.ILogger logger = options.LoggerFactory.CreateLogger("DebugOptionsStartup");

                // We need to let the PowerShell Context Service know that we are in a debug session
                // so that it doesn't send the powerShell/startDebugger message.
                _powerShellContextService = ServiceProvider.GetService<PowerShellContextService>();
                _powerShellContextService.IsDebugServerActive = true;

                // Needed to make sure PSReadLine's static properties are initialized in the pipeline thread.
                _powerShellContextService
                    .ExecuteScriptStringAsync("[System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor([Microsoft.PowerShell.PSConsoleReadLine].TypeHandle)")
                    .Wait();

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
            });
        }

        public void Dispose()
        {
            _powerShellContextService.IsDebugServerActive = false;
            _jsonRpcServer.Dispose();
            _serverStopped.SetResult(true);
        }

        public async Task WaitForShutdown()
        {
            await _serverStopped.Task;
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
