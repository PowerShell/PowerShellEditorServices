// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Xunit;
using static PowerShellEditorServices.Test.Handlers.RefactorFunctionTests;
using static PowerShellEditorServices.Test.Refactoring.RefactorUtilities;

namespace PowerShellEditorServices.Test.Handlers;

[Trait("Category", "PrepareRename")]
public class PrepareRenameHandlerTests : TheoryData<RenameSymbolParamsSerialized>
{
    private readonly WorkspaceService workspace = new(NullLoggerFactory.Instance);
    private readonly PrepareRenameHandler handler;
    public PrepareRenameHandlerTests()
    {
        workspace.WorkspaceFolders.Add(new WorkspaceFolder
        {
            Uri = DocumentUri.FromFileSystemPath(TestUtilities.GetSharedPath("Refactoring"))
        });
        // FIXME: Need to make a Mock<ILanguageServerFacade> to pass to the ExtensionService constructor

        handler = new(workspace, new fakeLspSendMessageRequestFacade("I Accept"), new fakeConfigurationService());
    }

    // TODO: Test an untitled document (maybe that belongs in E2E tests)

    [Theory]
    [ClassData(typeof(FunctionRenameTestData))]
    public async Task FindsSymbol(RenameSymbolParamsSerialized param)
    {
        // The test data is the PS script location. The handler expects 0-based line and column numbers.
        Position position = new(param.Line - 1, param.Column - 1);
        PrepareRenameParams testParams = new()
        {
            Position = position,
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath(
                    TestUtilities.GetSharedPath($"Refactoring/Functions/{param.FileName}")
                )
            }
        };

        RangeOrPlaceholderRange? result = await handler.Handle(testParams, CancellationToken.None);
        Assert.NotNull(result);
        Assert.NotNull(result.Range);
        Assert.True(result.Range.Contains(position));
    }
}

public class fakeLspSendMessageRequestFacade(string title) : ILanguageServerFacade
{
    public async Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        if (request is ShowMessageRequestParams)
        {
            return (TResponse)(object)new MessageActionItem { Title = title };
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public ITextDocumentLanguageServer TextDocument => throw new NotImplementedException();
    public INotebookDocumentLanguageServer NotebookDocument => throw new NotImplementedException();
    public IClientLanguageServer Client => throw new NotImplementedException();
    public IGeneralLanguageServer General => throw new NotImplementedException();
    public IWindowLanguageServer Window => throw new NotImplementedException();
    public IWorkspaceLanguageServer Workspace => throw new NotImplementedException();
    public IProgressManager ProgressManager => throw new NotImplementedException();
    public InitializeParams ClientSettings => throw new NotImplementedException();
    public InitializeResult ServerSettings => throw new NotImplementedException();
    public object GetService(Type serviceType) => throw new NotImplementedException();
    public IDisposable Register(Action<ILanguageServerRegistry> registryAction) => throw new NotImplementedException();
    public void SendNotification(string method) => throw new NotImplementedException();
    public void SendNotification<T>(string method, T @params) => throw new NotImplementedException();
    public void SendNotification(IRequest request) => throw new NotImplementedException();
    public IResponseRouterReturns SendRequest(string method) => throw new NotImplementedException();
    public IResponseRouterReturns SendRequest<T>(string method, T @params) => throw new NotImplementedException();
    public bool TryGetRequest(long id, out string method, out TaskCompletionSource<JToken> pendingTask) => throw new NotImplementedException();
}

public class fakeConfigurationService : ILanguageServerConfiguration
{
    public string this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool IsSupported => throw new NotImplementedException();

    public ILanguageServerConfiguration AddConfigurationItems(IEnumerable<ConfigurationItem> configurationItems) => throw new NotImplementedException();
    public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
    public Task<IConfiguration> GetConfiguration(params ConfigurationItem[] items) => throw new NotImplementedException();
    public IChangeToken GetReloadToken() => throw new NotImplementedException();
    public Task<IScopedConfiguration> GetScopedConfiguration(DocumentUri scopeUri, CancellationToken cancellationToken) => throw new NotImplementedException();
    public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    public ILanguageServerConfiguration RemoveConfigurationItems(IEnumerable<ConfigurationItem> configurationItems) => throw new NotImplementedException();
    public bool TryGetScopedConfiguration(DocumentUri scopeUri, out IScopedConfiguration configuration) => throw new NotImplementedException();
}
