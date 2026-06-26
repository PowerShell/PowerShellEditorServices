// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Extensions.Services;
using Microsoft.PowerShell.EditorServices.Services.Extension;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Test;
using Xunit;
using WorkspaceService = Microsoft.PowerShell.EditorServices.Services.WorkspaceService;

namespace PowerShellEditorServices.Test.Extensions
{
    [Trait("Category", "Extensions")]
    public class EditorExtensionServiceProviderTests : IAsyncLifetime
    {
        private PsesInternalHost psesHost;

        private WorkspaceService workspaceService;

        private EditorExtensionServiceProvider serviceProvider;

        public async Task InitializeAsync()
        {
            psesHost = await PsesHostFactory.Create(NullLoggerFactory.Instance);

            ExtensionService extensionService = new(
                languageServer: null,
                serviceProvider: null,
                editorOperations: null,
                executionService: psesHost);
            await extensionService.InitializeAsync();

            workspaceService = new WorkspaceService(NullLoggerFactory.Instance);

            ServiceCollection services = new();
            services.AddSingleton(extensionService);
            services.AddSingleton(workspaceService);

            serviceProvider = new EditorExtensionServiceProvider(services.BuildServiceProvider());
        }

        public async Task DisposeAsync() => await psesHost.StopAsync();

        [Fact]
        public void GetServiceByFullTypeNameReturnsService() =>
            Assert.Same(
                workspaceService,
                serviceProvider.GetService("Microsoft.PowerShell.EditorServices.Services.WorkspaceService"));

        [Fact]
        public void GetServiceByTypeNameAndAssemblyNameReturnsService() =>
            Assert.Same(
                workspaceService,
                serviceProvider.GetService(
                    "Microsoft.PowerShell.EditorServices.Services.WorkspaceService",
                    "Microsoft.PowerShell.EditorServices"));

        [Fact]
        public void GetServiceByAssemblyQualifiedNameReturnsService() =>
            Assert.Same(
                workspaceService,
                serviceProvider.GetServiceByAssemblyQualifiedName(
                    "Microsoft.PowerShell.EditorServices.Services.WorkspaceService, Microsoft.PowerShell.EditorServices"));
    }
}
