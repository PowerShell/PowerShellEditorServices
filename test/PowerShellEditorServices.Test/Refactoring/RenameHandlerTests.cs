// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using static PowerShellEditorServices.Test.Refactoring.RefactorUtilities;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using PowerShellEditorServices.Test.Shared.Refactoring;

namespace PowerShellEditorServices.Test.Handlers;
#pragma warning disable VSTHRD100 // XUnit handles async void with a custom SyncContext

[Trait("Category", "RenameHandlerFunction")]
public class RenameHandlerTests
{
    internal WorkspaceService workspace = new(NullLoggerFactory.Instance);

    private readonly RenameHandler testHandler;
    public RenameHandlerTests()
    {
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

    // Decided to keep this DAMP instead of DRY due to memberdata boundaries, duplicates with PrepareRenameHandler
    public static TheoryData<RenameTestTarget> VariableTestCases()
        => new(RefactorVariableTestCases.TestCases);

    public static TheoryData<RenameTestTarget> FunctionTestCases()
        => new(RefactorFunctionTestCases.TestCases);

    [Theory]
    [MemberData(nameof(VariableTestCases))]
    public async void RenamedSymbol(RenameTestTarget request)
    {
        string fileName = request.FileName;
        ScriptFile scriptFile = GetTestScript(fileName);

        WorkspaceEdit response = await testHandler.Handle(request.ToRenameParams(), CancellationToken.None);

        string expected = GetTestScript(fileName.Substring(0, fileName.Length - 4) + "Renamed.ps1").Contents;
        string actual = GetModifiedScript(scriptFile.Contents, response.Changes[request.ToRenameParams().TextDocument.Uri].ToArray());

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(FunctionTestCases))]
    public async void RenamedFunction(RenameTestTarget request)
    {
        string fileName = request.FileName;
        ScriptFile scriptFile = GetTestScript(fileName);

        WorkspaceEdit response = await testHandler.Handle(request.ToRenameParams(), CancellationToken.None);

        string expected = GetTestScript(fileName.Substring(0, fileName.Length - 4) + "Renamed.ps1").Contents;
        string actual = GetModifiedScript(scriptFile.Contents, response.Changes[request.ToRenameParams().TextDocument.Uri].ToArray());

        Assert.Equal(expected, actual);
    }

    private ScriptFile GetTestScript(string fileName) =>
        workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Refactoring", "Functions", fileName)));
}

public static partial class RenameTestTargetExtensions
{
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
