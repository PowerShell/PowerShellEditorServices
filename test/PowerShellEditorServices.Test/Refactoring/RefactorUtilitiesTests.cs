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
using System.Management.Automation.Language;
using static PowerShellEditorServices.Test.Refactoring.RefactorUtilities;
using Microsoft.PowerShell.EditorServices.Refactoring;
using PowerShellEditorServices.Test.Shared.Refactoring.Utilities;

namespace PowerShellEditorServices.Test.Refactoring
{
    [Trait("Category", "RefactorUtilities")]
    public class RefactorUtilitiesTests : IAsyncLifetime
    {
        private PsesInternalHost psesHost;
        private WorkspaceService workspace;

        public async Task InitializeAsync()
        {
            psesHost = await PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
        }

        public async Task DisposeAsync() => await Task.Run(psesHost.StopAsync);
        private ScriptFile GetTestScript(string fileName) => workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Refactoring", "Utilities", fileName)));


        public class GetAstShouldDetectTestData : TheoryData<RenameSymbolParamsSerialized, int, int>
        {
            public GetAstShouldDetectTestData()
            {
                Add(new RenameSymbolParamsSerialized(RenameUtilitiesData.GetVariableExpressionAst), 15, 1);
                Add(new RenameSymbolParamsSerialized(RenameUtilitiesData.GetVariableExpressionStartAst), 15, 1);
                Add(new RenameSymbolParamsSerialized(RenameUtilitiesData.GetVariableWithinParameterAst), 3, 17);
                Add(new RenameSymbolParamsSerialized(RenameUtilitiesData.GetHashTableKey), 16, 5);
                Add(new RenameSymbolParamsSerialized(RenameUtilitiesData.GetVariableWithinCommandAst), 6, 28);
                Add(new RenameSymbolParamsSerialized(RenameUtilitiesData.GetCommandParameterAst), 21, 10);
                Add(new RenameSymbolParamsSerialized(RenameUtilitiesData.GetFunctionDefinitionAst), 1, 1);
            }
        }

        [Theory]
        [ClassData(typeof(GetAstShouldDetectTestData))]
        public void GetAstShouldDetect(RenameSymbolParamsSerialized s, int l, int c)
        {
            ScriptFile scriptFile = GetTestScript(s.FileName);
            Ast symbol = Utilities.GetAst(s.Line, s.Column, scriptFile.ScriptAst);
            // Assert the Line and Column is what is expected
            Assert.Equal(l, symbol.Extent.StartLineNumber);
            Assert.Equal(c, symbol.Extent.StartColumnNumber);
        }

        [Fact]
        public void GetVariableUnderFunctionDef()
        {
            RenameSymbolParams request = new()
            {
                Column = 5,
                Line = 2,
                RenameTo = "Renamed",
                FileName = "TestDetectionUnderFunctionDef.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line, request.Column, scriptFile.ScriptAst);
            Assert.IsType<VariableExpressionAst>(symbol);
            Assert.Equal(2, symbol.Extent.StartLineNumber);
            Assert.Equal(5, symbol.Extent.StartColumnNumber);

        }
        [Fact]
        public void AssertContainsDotSourcingTrue()
        {
            ScriptFile scriptFile = GetTestScript("TestDotSourcingTrue.ps1");
            Assert.True(Utilities.AssertContainsDotSourced(scriptFile.ScriptAst));
        }
        [Fact]
        public void AssertContainsDotSourcingFalse()
        {
            ScriptFile scriptFile = GetTestScript("TestDotSourcingFalse.ps1");
            Assert.False(Utilities.AssertContainsDotSourced(scriptFile.ScriptAst));
        }
    }
}
