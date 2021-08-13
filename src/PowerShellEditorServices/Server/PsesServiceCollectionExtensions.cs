// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Extension;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.Template;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Server
{
    internal static class PsesServiceCollectionExtensions
    {
        public static IServiceCollection AddPsesLanguageServices(
            this IServiceCollection collection,
            HostStartupInfo hostStartupInfo)
        {
            return collection
                .AddSingleton<HostStartupInfo>(hostStartupInfo)
                .AddSingleton<WorkspaceService>()
                .AddSingleton<SymbolsService>()
                .AddSingleton<InternalHost>()
                .AddSingleton<IRunspaceContext>(
                    (provider) => provider.GetService<InternalHost>())
                .AddSingleton<PowerShellExecutionService>()
                .AddSingleton<ConfigurationService>()
                .AddSingleton<IPowerShellDebugContext>(
                    (provider) => provider.GetService<InternalHost>().DebugContext)
                .AddSingleton<TemplateService>()
                .AddSingleton<EditorOperationsService>()
                .AddSingleton<RemoteFileManagerService>()
                .AddSingleton<ExtensionService>((provider) =>
                    {
                        var extensionService = new ExtensionService(
                            provider.GetService<ILanguageServerFacade>(),
                            provider,
                            provider.GetService<EditorOperationsService>(),
                            provider.GetService<PowerShellExecutionService>());

                        // This is where we create the $psEditor variable
                        // so that when the console is ready, it will be available
                        // TODO: Improve the sequencing here so that:
                        //  - The variable is guaranteed to be initialized when the console first appears
                        //  - Any errors that occur are handled rather than lost by the unawaited task
                        extensionService.InitializeAsync();

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
            return collection
                .AddSingleton(languageServiceProvider.GetService<EditorServicesConsolePSHost>())
                .AddSingleton<IRunspaceContext>(languageServiceProvider.GetService<InternalHost>())
                .AddSingleton<IPowerShellDebugContext>(languageServiceProvider.GetService<InternalHost>().DebugContext)
                .AddSingleton(languageServiceProvider.GetService<PowerShellExecutionService>())
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
