//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Engine.Handlers;
using Microsoft.PowerShell.EditorServices.Engine.Hosting;
using Microsoft.PowerShell.EditorServices.Engine.Services;
using Microsoft.PowerShell.EditorServices.Engine.Services.PowerShellContext;
using OmniSharp.Extensions.DebugAdapter.Protocol.Serialization;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Microsoft.PowerShell.EditorServices.Engine.Server
{
    internal class PsesDebugServer
    {
        protected readonly ILoggerFactory _loggerFactory;
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly TaskCompletionSource<bool> _serverStart;
        

        private IJsonRpcServer _jsonRpcServer;

        internal PsesDebugServer(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream)
        {
            _loggerFactory = factory;
            _inputStream = inputStream;
            _outputStream = outputStream;
            _serverStart = new TaskCompletionSource<bool>();

        }

        public async Task StartAsync()
        {
            _jsonRpcServer = await JsonRpcServer.From(options =>
            {
                options.Serializer = new DapProtocolSerializer();
                options.Reciever = new DapReciever();
                options.LoggerFactory = _loggerFactory;
                ILogger logger = options.LoggerFactory.CreateLogger("DebugOptionsStartup");
                options.AddHandler<PowershellInitializeHandler>();
                // options.Services = new ServiceCollection()
                //     .AddSingleton<WorkspaceService>()
                //     .AddSingleton<SymbolsService>()
                //     .AddSingleton<ConfigurationService>()
                //     .AddSingleton<PowerShellContextService>(
                //         (provider) =>
                //             GetFullyInitializedPowerShellContext(
                //                 provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer>(),
                //                 _profilePaths))
                //     .AddSingleton<TemplateService>()
                //     .AddSingleton<EditorOperationsService>()
                //     .AddSingleton<ExtensionService>(
                //         (provider) =>
                //         {
                //             var extensionService = new ExtensionService(
                //                 provider.GetService<PowerShellContextService>(),
                //                 provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer>());
                //             extensionService.InitializeAsync(
                //                 serviceProvider: provider,
                //                 editorOperations: provider.GetService<EditorOperationsService>())
                //                 .Wait();
                //             return extensionService;
                //         })
                //     .AddSingleton<AnalysisService>(
                //         (provider) =>
                //         {
                //             return AnalysisService.Create(
                //                 provider.GetService<ConfigurationService>(),
                //                 provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer>(),
                //                 options.LoggerFactory.CreateLogger<AnalysisService>());
                //         });

                options
                    .WithInput(_inputStream)
                    .WithOutput(_outputStream);

                logger.LogInformation("Adding handlers");

                logger.LogInformation("Handlers added");
            });
        }

        public async Task WaitForShutdown()
        {
            await _serverStart.Task;
            //await _languageServer.;
        }
    }
}
