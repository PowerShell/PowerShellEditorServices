// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;

namespace Microsoft.PowerShell.EditorServices.Server
{
    internal static class PsesServiceCollectionExtensions
    {
        public static IServiceCollection AddPsesLanguageServices(
            this IServiceCollection collection,
            HostStartupInfo hostStartupInfo)
        {
            return collection.AddSingleton<WorkspaceService>()
                .AddSingleton<SymbolsService>()
                .AddSingleton<ConfigurationService>()
                .AddSingleton<PowerShellContextService>(
                    (provider) =>
                        PowerShellContextService.Create(
                            provider.GetService<ILoggerFactory>(),
                            provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServerFacade>(),
                            hostStartupInfo))
                .AddSingleton<TemplateService>()
                .AddSingleton<EditorOperationsService>()
                .AddSingleton<RemoteFileManagerService>()
                .AddSingleton<ExtensionService>(
                    (provider) =>
                    {
                        var extensionService = new ExtensionService(
                            provider.GetService<PowerShellContextService>(),
                            provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServerFacade>());
                        extensionService.InitializeAsync(
                            serviceProvider: provider,
                            editorOperations: provider.GetService<EditorOperationsService>())
                            .Wait();
                        return extensionService;
                    })
                .AddSingleton<AnalysisService>();
        }

        public static IServiceCollection AddPsesDebugServices(
            this IServiceCollection collection,
            IServiceProvider languageServiceProvider,
            PsesDebugServer psesDebugServer,
            bool useTempSession)
        {
            return collection.AddSingleton(languageServiceProvider.GetService<PowerShellContextService>())
                .AddSingleton(languageServiceProvider.GetService<WorkspaceService>())
                .AddSingleton(languageServiceProvider.GetService<RemoteFileManagerService>())
                .AddSingleton<PsesDebugServer>(psesDebugServer)
                .AddSingleton<DebugService>()
                .AddSingleton<BreakpointService>()
                .AddSingleton<DebugStateService>(new DebugStateService
                {
                     OwnsEditorSession = useTempSession
                })
                .AddSingleton<DebugEventHandlerService>();
        }
    }
}
