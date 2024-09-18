// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
using PowerShellEditorServices.Test.Shared.Refactoring;
using Xunit;
using Xunit.Abstractions;

namespace PowerShellEditorServices.Test.Handlers;

[Trait("Category", "PrepareRename")]
public class PrepareRenameHandlerTests
{
    private readonly PrepareRenameHandler testHandler;

    public PrepareRenameHandlerTests()
    {
        WorkspaceService workspace = new(NullLoggerFactory.Instance);
        workspace.WorkspaceFolders.Add(new WorkspaceFolder
        {
            Uri = DocumentUri.FromFileSystemPath(TestUtilities.GetSharedPath("Refactoring"))
        });

        testHandler = new
        (
            new RenameService
            (
                workspace,
                new fakeLspSendMessageRequestFacade("I Accept"),
                new fakeConfigurationService()
            )
        );
    }

    /// <summary>
    /// Convert test cases into theory data. This keeps us from needing xunit in the test data project
    /// This type has a special ToString to add a data-driven test name which is why we dont convert directly to the param type first
    /// </summary>
    public static TheoryData<RenameTestTargetSerializable> VariableTestCases()
        => new(RefactorVariableTestCases.TestCases.Select(RenameTestTargetSerializable.FromRenameTestTarget));

    public static TheoryData<RenameTestTargetSerializable> FunctionTestCases()
        => new(RefactorFunctionTestCases.TestCases.Select(RenameTestTargetSerializable.FromRenameTestTarget));

    [Theory]
    [MemberData(nameof(FunctionTestCases))]
    public async Task FindsFunction(RenameTestTarget s)
    {
        PrepareRenameParams testParams = s.ToPrepareRenameParams("Functions");

        RangeOrPlaceholderRange? result = await testHandler.Handle(testParams, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result?.DefaultBehavior?.DefaultBehavior);
    }

    [Theory]
    [MemberData(nameof(VariableTestCases))]
    public async Task FindsVariable(RenameTestTarget s)
    {
        PrepareRenameParams testParams = s.ToPrepareRenameParams("Variables");

        RangeOrPlaceholderRange? result = await testHandler.Handle(testParams, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result?.DefaultBehavior?.DefaultBehavior);
    }

    // TODO: Bad Path Tests (strings, parameters, etc.)
}

public static partial class RenameTestTargetExtensions
{
    public static PrepareRenameParams ToPrepareRenameParams(this RenameTestTarget testCase, string baseFolder)
        => new()
        {
            Position = new ScriptPositionAdapter(Line: testCase.Line, Column: testCase.Column),
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath(
                    TestUtilities.GetSharedPath($"Refactoring/{baseFolder}/{testCase.FileName}")
                )
            }
        };
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

public static partial class RenameTestTargetExtensions
{
    /// <summary>
    /// Extension Method to convert a RenameTestTarget to a RenameParams. Needed because RenameTestTarget is in a separate project.
    /// </summary>
    public static RenameParams ToRenameParams(this RenameTestTarget testCase)
        => new()
        {
            Position = new ScriptPositionAdapter(Line: testCase.Line, Column: testCase.Column),
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath(
                    TestUtilities.GetSharedPath($"Refactoring/Functions/{testCase.FileName}")
                )
            },
            NewName = testCase.NewName
        };
}

/// <summary>
/// This is necessary for the MS test explorer to display the test cases
/// Ref:
/// </summary>
public class RenameTestTargetSerializable : RenameTestTarget, IXunitSerializable
{
    public RenameTestTargetSerializable() : base() { }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(FileName), FileName);
        info.AddValue(nameof(Line), Line);
        info.AddValue(nameof(Column), Column);
        info.AddValue(nameof(NewName), NewName);
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        FileName = info.GetValue<string>(nameof(FileName));
        Line = info.GetValue<int>(nameof(Line));
        Column = info.GetValue<int>(nameof(Column));
        NewName = info.GetValue<string>(nameof(NewName));
    }

    public static RenameTestTargetSerializable FromRenameTestTarget(RenameTestTarget t)
        => new RenameTestTargetSerializable()
        {
            FileName = t.FileName,
            Column = t.Column,
            Line = t.Line,
            NewName = t.NewName
        };
}
