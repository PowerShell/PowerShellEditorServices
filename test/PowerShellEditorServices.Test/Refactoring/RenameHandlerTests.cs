// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using static PowerShellEditorServices.Test.Refactoring.RefactorUtilities;
using System.Linq;
using System.Threading;
using Xunit;
using PowerShellEditorServices.Test.Shared.Refactoring;
using System.Threading.Tasks;

namespace PowerShellEditorServices.Test.Handlers;
#pragma warning disable VSTHRD100 // XUnit handles async void with a custom SyncContext

[Trait("Category", "RenameHandlerFunction")]
public class RenameHandlerTests
{
    private readonly WorkspaceService workspace = new(NullLoggerFactory.Instance);

    private readonly RenameHandler testHandler;
    private readonly PrepareRenameHandler testPrepareHandler;
    public RenameHandlerTests()
    {
        workspace.WorkspaceFolders.Add(new WorkspaceFolder
        {
            Uri = DocumentUri.FromFileSystemPath(TestUtilities.GetSharedPath("Refactoring"))
        });

        RenameService renameService = new
        (
            workspace,
            new FakeLspSendMessageRequestFacade("I Accept"),
            new EmptyConfiguration()
        )
        {
            DisclaimerAcceptedForSession = true //Disables UI prompts
        };
        testHandler = new(renameService);
        testPrepareHandler = new(renameService);
    }

    // Decided to keep this DAMP instead of DRY due to memberdata boundaries, duplicates with PrepareRenameHandler
    public static TheoryData<RenameTestTargetSerializable> VariableTestCases()
        => new(RefactorVariableTestCases.TestCases.Select(RenameTestTargetSerializable.FromRenameTestTarget));

    public static TheoryData<RenameTestTargetSerializable> FunctionTestCases()
        => new(RefactorFunctionTestCases.TestCases.Select(RenameTestTargetSerializable.FromRenameTestTarget));

    [Theory]
    [MemberData(nameof(FunctionTestCases))]
    public async Task RenamedFunction(RenameTestTarget s)
    {
        RenameParams request = s.ToRenameParams("Functions");
        WorkspaceEdit response;
        try
        {
            response = await testHandler.Handle(request, CancellationToken.None);
        }
        catch (HandlerErrorException err)
        {
            Assert.True(s.ShouldThrow, $"Unexpected HandlerErrorException: {err.Message}");
            return;
        }
        if (s.ShouldFail)
        {
            Assert.Null(response);
            return;
        }

        DocumentUri testScriptUri = request.TextDocument.Uri;

        string expected = workspace.GetFile
        (
            testScriptUri.ToString().Substring(0, testScriptUri.ToString().Length - 4) + "Renamed.ps1"
        ).Contents;

        ScriptFile scriptFile = workspace.GetFile(testScriptUri);

        Assert.NotEmpty(response.Changes[testScriptUri]);

        string actual = GetModifiedScript(scriptFile.Contents, response.Changes[testScriptUri].ToArray());

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(VariableTestCases))]
    public async Task RenamedVariable(RenameTestTarget s)
    {
        RenameParams request = s.ToRenameParams("Variables");
        WorkspaceEdit response;
        try
        {
            response = await testHandler.Handle(request, CancellationToken.None);
        }
        catch (HandlerErrorException err)
        {
            Assert.True(s.ShouldThrow, $"Unexpected HandlerErrorException: {err.Message}");
            return;
        }
        if (s.ShouldFail)
        {
            Assert.Null(response);
            return;
        }
        DocumentUri testScriptUri = request.TextDocument.Uri;

        string expected = workspace.GetFile
        (
            testScriptUri.ToString().Substring(0, testScriptUri.ToString().Length - 4) + "Renamed.ps1"
        ).Contents;

        ScriptFile scriptFile = workspace.GetFile(testScriptUri);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Changes[testScriptUri]);

        string actual = GetModifiedScript(scriptFile.Contents, response.Changes[testScriptUri].ToArray());

        Assert.Equal(expected, actual);
    }

    public enum RegistrationHandlerKind
    {
        Rename,
        PrepareRename
    }

    // A null prepareSupport represents the client omitting the capability entirely (framework hands us null).
    public static TheoryData<RegistrationHandlerKind, bool?, bool> RegistrationOptionsTestCases() => new()
    {
        { RegistrationHandlerKind.Rename, null, false },
        { RegistrationHandlerKind.Rename, true, true },
        { RegistrationHandlerKind.PrepareRename, null, false },
        { RegistrationHandlerKind.PrepareRename, true, true }
    };

    [Theory]
    [MemberData(nameof(RegistrationOptionsTestCases))]
    public void GetRegistrationOptionsReflectsPrepareSupport(RegistrationHandlerKind handlerKind, bool? prepareSupport, bool expectedPrepareProvider)
    {
        RenameCapability capability = prepareSupport is bool ps
            ? new RenameCapability { PrepareSupport = ps }
            : null;

        Func<RenameCapability, ClientCapabilities, RenameRegistrationOptions> getRegistrationOptions = handlerKind switch
        {
            RegistrationHandlerKind.Rename => testHandler.GetRegistrationOptions,
            RegistrationHandlerKind.PrepareRename => testPrepareHandler.GetRegistrationOptions,
            _ => throw new ArgumentOutOfRangeException(nameof(handlerKind))
        };

        RenameRegistrationOptions opts = getRegistrationOptions(capability, new ClientCapabilities());

        Assert.NotNull(opts);
        Assert.Equal(expectedPrepareProvider, opts.PrepareProvider);
    }
}
