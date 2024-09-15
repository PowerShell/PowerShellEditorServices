// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Xunit;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Refactoring;
using PowerShellEditorServices.Test.Shared.Refactoring.Functions;
using static PowerShellEditorServices.Test.Refactoring.RefactorUtilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace PowerShellEditorServices.Test.Handlers;

[Trait("Category", "RenameHandlerFunction")]
public class RefactorFunctionTests : IAsyncLifetime
{
    private PsesInternalHost psesHost;
    private WorkspaceService workspace;
    public async Task InitializeAsync()
    {
        psesHost = await PsesHostFactory.Create(NullLoggerFactory.Instance);
        workspace = new WorkspaceService(NullLoggerFactory.Instance);
    }

    public async Task DisposeAsync() => await Task.Run(psesHost.StopAsync);
    private ScriptFile GetTestScript(string fileName) => workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Refactoring", "Functions", fileName)));

    internal static string GetRenamedFunctionScriptContent(ScriptFile scriptFile, RenameSymbolParamsSerialized request, SymbolReference symbol)
    {
        IterativeFunctionRename visitor = new(symbol.NameRegion.Text,
                                    request.RenameTo,
                                    symbol.ScriptRegion.StartLineNumber,
                                    symbol.ScriptRegion.StartColumnNumber,
                                    scriptFile.ScriptAst);
        visitor.Visit(scriptFile.ScriptAst);
        TextEdit[] changes = visitor.Modifications.ToArray();
        return GetModifiedScript(scriptFile.Contents, changes);
    }

    public class FunctionRenameTestData : TheoryData<RenameSymbolParamsSerialized>
    {
        public FunctionRenameTestData()
        {

            // Simple
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionsSingle));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionWithInternalCalls));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionCmdlet));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionScriptblock));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionCallWIthinStringExpression));
            // Loops
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionLoop));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionForeach));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionForeachObject));
            // Nested
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionInnerIsNested));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionOuterHasNestedFunction));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionInnerIsNested));
            // Multi Occurance
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionMultipleOccurrences));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionSameName));
            Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionNestedRedefinition));
        }
    }

    [Theory]
    [ClassData(typeof(FunctionRenameTestData))]
    public void Rename(RenameSymbolParamsSerialized s)
    {
        RenameSymbolParamsSerialized request = s;
        ScriptFile scriptFile = GetTestScript(request.FileName);
        ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
        SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
            request.Line,
            request.Column
        );

        string modifiedcontent = GetRenamedFunctionScriptContent(scriptFile, request, symbol);


        Assert.Equal(expectedContent.Contents, modifiedcontent);
    }
}

