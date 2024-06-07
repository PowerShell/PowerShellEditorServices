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
using Microsoft.PowerShell.EditorServices.Handlers;
using Xunit;
using PowerShellEditorServices.Test.Shared.Refactoring.Variables;
using static PowerShellEditorServices.Test.Refactoring.RefactorUtilities;
using Microsoft.PowerShell.EditorServices.Refactoring;

namespace PowerShellEditorServices.Test.Refactoring
{
    [Trait("Category", "RenameVariables")]
    public class RefactorVariableTests : IAsyncLifetime

    {
        private PsesInternalHost psesHost;
        private WorkspaceService workspace;
        public async Task InitializeAsync()
        {
            psesHost = await PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
        }
        public async Task DisposeAsync() => await Task.Run(psesHost.StopAsync);
        private ScriptFile GetTestScript(string fileName) => workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Refactoring", "Variables", fileName)));

        internal static string TestRenaming(ScriptFile scriptFile, RenameSymbolParamsSerialized request)
        {

            IterativeVariableRename iterative = new(request.RenameTo,
                                        request.Line,
                                        request.Column,
                                        scriptFile.ScriptAst);
            iterative.Visit(scriptFile.ScriptAst);
            ModifiedFileResponse changes = new(request.FileName)
            {
                Changes = iterative.Modifications
            };
            return GetModifiedScript(scriptFile.Contents, changes);
        }
        public class VariableRenameTestData : TheoryData<RenameSymbolParamsSerialized>
        {
            public VariableRenameTestData()
            {
                Add(new RenameSymbolParamsSerialized(RenameVariableData.SimpleVariableAssignment));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableRedefinition));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableNestedScopeFunction));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableInLoop));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableInPipeline));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableInScriptblock));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableInScriptblockScoped));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariablewWithinHastableExpression));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableNestedFunctionScriptblock));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableWithinCommandAstScriptBlock));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableWithinForeachObject));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableusedInWhileLoop));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableInParam));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableCommandParameter));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableCommandParameterReverse));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableScriptWithParamBlock));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableNonParam));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableParameterCommandWithSameName));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableCommandParameterSplattedFromCommandAst));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableCommandParameterSplattedFromSplat));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableInForeachDuplicateAssignment));
                Add(new RenameSymbolParamsSerialized(RenameVariableData.VariableInForloopDuplicateAssignment));
            }
        }

        [Theory]
        [ClassData(typeof(VariableRenameTestData))]
        public void Rename(RenameSymbolParamsSerialized s)
        {
            RenameSymbolParamsSerialized request = s;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
    }

}
