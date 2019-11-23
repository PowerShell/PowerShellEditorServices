//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace Microsoft.PowerShell.EditorServices.Server
{
    internal class PsesLanguageServer
    {
        internal ILoggerFactory LoggerFactory { get; private set; }

        internal ILanguageServer LanguageServer { get; private set; }

        private readonly LogLevel _minimumLogLevel;
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly HostStartupInfo _hostDetails;
        private readonly TaskCompletionSource<bool> _serverStart;

        public PsesLanguageServer(
            ILoggerFactory factory,
            LogLevel minimumLogLevel,
            Stream inputStream,
            Stream outputStream,
            HostStartupInfo hostDetails)
        {
            LoggerFactory = factory;
            _minimumLogLevel = minimumLogLevel;
            _inputStream = inputStream;
            _outputStream = outputStream;
            _hostDetails = hostDetails;
            _serverStart = new TaskCompletionSource<bool>();
        }

        public async Task StartAsync()
        {
            LanguageServer = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
            {
                options
                    .WithInput(_inputStream)
                    .WithOutput(_outputStream)
                    .WithServices(serviceCollection => serviceCollection
                        .AddPsesLanguageServices(_hostDetails))
                    .ConfigureLogging(builder => builder
                        .AddSerilog(Log.Logger)
                        .AddLanguageServer(LogLevel.Trace)
                        .SetMinimumLevel(LogLevel.Trace))
                    .WithHandler<WorkspaceSymbolsHandler>()
                    .WithHandler<TextDocumentHandler>()
                    .WithHandler<GetVersionHandler>()
                    .WithHandler<ConfigurationHandler>()
                    .WithHandler<FoldingRangeHandler>()
                    .WithHandler<DocumentFormattingHandler>()
                    .WithHandler<DocumentRangeFormattingHandler>()
                    .WithHandler<ReferencesHandler>()
                    .WithHandler<DocumentSymbolHandler>()
                    .WithHandler<DocumentHighlightHandler>()
                    .WithHandler<PSHostProcessAndRunspaceHandlers>()
                    .WithHandler<CodeLensHandlers>()
                    .WithHandler<CodeActionHandler>()
                    .WithHandler<InvokeExtensionCommandHandler>()
                    .WithHandler<CompletionHandler>()
                    .WithHandler<HoverHandler>()
                    .WithHandler<SignatureHelpHandler>()
                    .WithHandler<DefinitionHandler>()
                    .WithHandler<TemplateHandlers>()
                    .WithHandler<GetCommentHelpHandler>()
                    .WithHandler<EvaluateHandler>()
                    .WithHandler<GetCommandHandler>()
                    .WithHandler<ShowHelpHandler>()
                    .WithHandler<ExpandAliasHandler>()
                    .OnInitialize(
                        async (languageServer, request) =>
                        {
                            var serviceProvider = languageServer.Services;
                            var workspaceService = serviceProvider.GetService<WorkspaceService>();

                            // Grab the workspace path from the parameters
                            if (request.RootUri != null)
                            {
                                workspaceService.WorkspacePath = workspaceService.ResolveFilePath(request.RootUri.ToString());
                            }

                            // Set the working directory of the PowerShell session to the workspace path
                            if (workspaceService.WorkspacePath != null
                                && Directory.Exists(workspaceService.WorkspacePath))
                            {
                                await serviceProvider.GetService<PowerShellContextService>().SetWorkingDirectoryAsync(
                                    workspaceService.WorkspacePath,
                                    isPathAlreadyEscaped: false);
                            }
                        });
            });
        }

        public async Task WaitForShutdown()
        {
            await _serverStart.Task;
            await LanguageServer.WaitForExit;
        }
    }
}
