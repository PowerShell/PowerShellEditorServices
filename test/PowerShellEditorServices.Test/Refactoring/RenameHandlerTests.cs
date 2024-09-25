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
                new EmptyConfiguration(),
                disclaimerAcceptedForSession: true //Disables UI prompts
            )
        );
    }

    // Decided to keep this DAMP instead of DRY due to memberdata boundaries, duplicates with PrepareRenameHandler
    public static TheoryData<RenameTestTargetSerializable> VariableTestCases()
        => new(RefactorVariableTestCases.TestCases.Select(RenameTestTargetSerializable.FromRenameTestTarget));

    public static TheoryData<RenameTestTargetSerializable> FunctionTestCases()
        => new(RefactorFunctionTestCases.TestCases.Select(RenameTestTargetSerializable.FromRenameTestTarget));

    [Theory]
    [MemberData(nameof(FunctionTestCases))]
    public async void RenamedFunction(RenameTestTarget s)
    {
        RenameParams request = s.ToRenameParams("Functions");
        WorkspaceEdit response = await testHandler.Handle(request, CancellationToken.None);
        DocumentUri testScriptUri = request.TextDocument.Uri;

        string expected = workspace.GetFile
        (
            testScriptUri.ToString().Substring(0, testScriptUri.ToString().Length - 4) + "Renamed.ps1"
        ).Contents;

        ScriptFile scriptFile = workspace.GetFile(testScriptUri);

        string actual = GetModifiedScript(scriptFile.Contents, response.Changes[testScriptUri].ToArray());

        Assert.NotEmpty(response.Changes[testScriptUri]);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(VariableTestCases))]
    public async void RenamedVariable(RenameTestTarget s)
    {
        RenameParams request = s.ToRenameParams("Variables");
        WorkspaceEdit response = await testHandler.Handle(request, CancellationToken.None);
        DocumentUri testScriptUri = request.TextDocument.Uri;

        string expected = workspace.GetFile
        (
            testScriptUri.ToString().Substring(0, testScriptUri.ToString().Length - 4) + "Renamed.ps1"
        ).Contents;

        ScriptFile scriptFile = workspace.GetFile(testScriptUri);

        string actual = GetModifiedScript(scriptFile.Contents, response.Changes[testScriptUri].ToArray());

        Assert.Equal(expected, actual);
    }
}
