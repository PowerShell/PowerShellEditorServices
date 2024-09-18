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
    public static TheoryData<RenameTestTargetSerializable> VariableTestCases()
        => new(RefactorVariableTestCases.TestCases.Select(RenameTestTargetSerializable.FromRenameTestTarget));

    public static TheoryData<RenameTestTargetSerializable> FunctionTestCases()
        => new(RefactorFunctionTestCases.TestCases.Select(RenameTestTargetSerializable.FromRenameTestTarget));

    [Theory]
    [MemberData(nameof(VariableTestCases))]
    public async void RenamedSymbol(RenameTestTarget s)
    {
        string fileName = s.FileName;
        ScriptFile scriptFile = GetTestScript(fileName);

        WorkspaceEdit response = await testHandler.Handle(s.ToRenameParams(), CancellationToken.None);

        string expected = GetTestScript(fileName.Substring(0, fileName.Length - 4) + "Renamed.ps1").Contents;
        string actual = GetModifiedScript(scriptFile.Contents, response.Changes[s.ToRenameParams().TextDocument.Uri].ToArray());

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(FunctionTestCases))]
    public async void RenamedFunction(RenameTestTarget s)
    {
        string fileName = s.FileName;
        ScriptFile scriptFile = GetTestScript(fileName);

        WorkspaceEdit response = await testHandler.Handle(s.ToRenameParams(), CancellationToken.None);

        string expected = GetTestScript(fileName.Substring(0, fileName.Length - 4) + "Renamed.ps1").Contents;
        string actual = GetModifiedScript(scriptFile.Contents, response.Changes[s.ToRenameParams().TextDocument.Uri].ToArray());

        Assert.Equal(expected, actual);
    }

    private ScriptFile GetTestScript(string fileName) =>
        workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Refactoring", "Functions", fileName)));
}
