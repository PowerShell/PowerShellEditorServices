//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
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
    public class PsesDebugServer : IDisposable
    {
        protected readonly ILoggerFactory _loggerFactory;
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;

        private IJsonRpcServer _jsonRpcServer;

        public PsesDebugServer(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream)
        {
            _loggerFactory = factory;
            _inputStream = inputStream;
            _outputStream = outputStream;
        }

        public async Task StartAsync(IServiceProvider languageServerServiceProvider)
        {
            _jsonRpcServer = await JsonRpcServer.From(options =>
            {
                options.Serializer = new DapProtocolSerializer();
                options.Reciever = new DapReciever();
                options.LoggerFactory = _loggerFactory;
                ILogger logger = options.LoggerFactory.CreateLogger("DebugOptionsStartup");
                options.Services = new ServiceCollection()
                    .AddSingleton(languageServerServiceProvider.GetService<PowerShellContextService>())
                    .AddSingleton(languageServerServiceProvider.GetService<WorkspaceService>())
                    .AddSingleton(languageServerServiceProvider.GetService<RemoteFileManagerService>())
                    .AddSingleton<PsesDebugServer>(this)
                    .AddSingleton<DebugService>()
                    .AddSingleton<DebugStateService>()
                    .AddSingleton<DebugEventHandlerService>();

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
            _jsonRpcServer.Dispose();
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
