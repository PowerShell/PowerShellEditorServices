// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using static PowerShellEditorServices.Handlers.Test.RefactorFunctionTests;
using static PowerShellEditorServices.Test.Refactoring.RefactorUtilities;

namespace PowerShellEditorServices.Handlers.Test;

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
        handler = new(workspace);
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

        RangeOrPlaceholderRange result = await handler.Handle(testParams, CancellationToken.None);
        Assert.NotNull(result);
        Assert.NotNull(result.Range);
        Assert.True(result.Range.Contains(position));
    }
}
