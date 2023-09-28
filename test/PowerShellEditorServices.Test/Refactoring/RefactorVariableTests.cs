// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Handlers;
using Xunit;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using PowerShellEditorServices.Test.Shared.Refactoring.Variables;
using Microsoft.PowerShell.EditorServices.Refactoring;

namespace PowerShellEditorServices.Test.Refactoring
{
    [Trait("Category", "RenameVariables")]
    public class RefactorVariableTests : IDisposable

    {
        private readonly PsesInternalHost psesHost;
        private readonly WorkspaceService workspace;
        public void Dispose()
        {
#pragma warning disable VSTHRD002
            psesHost.StopAsync().Wait();
#pragma warning restore VSTHRD002
            GC.SuppressFinalize(this);
        }
        private ScriptFile GetTestScript(string fileName) => workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Refactoring\\Variables", fileName)));

        internal static string GetModifiedScript(string OriginalScript, ModifiedFileResponse Modification)
        {
            Modification.Changes.Sort((a, b) =>
            {
                if (b.StartLine == a.StartLine)
                {
                    return b.EndColumn - a.EndColumn;
                }
                return b.StartLine - a.StartLine;

            });
            string[] Lines = OriginalScript.Split(
                            new string[] { Environment.NewLine },
                            StringSplitOptions.None);

            foreach (TextChange change in Modification.Changes)
            {
                string TargetLine = Lines[change.StartLine];
                string begin = TargetLine.Substring(0, change.StartColumn);
                string end = TargetLine.Substring(change.EndColumn);
                Lines[change.StartLine] = begin + change.NewText + end;
            }

            return string.Join(Environment.NewLine, Lines);
        }

        internal static string TestRenaming(ScriptFile scriptFile, RenameSymbolParams request, SymbolReference symbol)
        {

            VariableRename visitor = new(symbol.NameRegion.Text,
                                        request.RenameTo,
                                        symbol.ScriptRegion.StartLineNumber,
                                        symbol.ScriptRegion.StartColumnNumber,
                                        scriptFile.ScriptAst);
            scriptFile.ScriptAst.Visit(visitor);
            ModifiedFileResponse changes = new(request.FileName)
            {
                Changes = visitor.Modifications
            };
            return GetModifiedScript(scriptFile.Contents, changes);
        }
        public RefactorVariableTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
        }
        [Fact]
        public void RefactorVariableSingle()
        {
            RenameSymbolParams request = RenameVariableData.SimpleVariableAssignment;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RefactorVariableNestedScopeFunction()
        {
            RenameSymbolParams request = RenameVariableData.VariableNestedScopeFunction;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RefactorVariableInPipeline()
        {
            RenameSymbolParams request = RenameVariableData.VariableInPipeline;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RefactorVariableInScriptBlock()
        {
            RenameSymbolParams request = RenameVariableData.VariableInScriptblock;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RefactorVariableInScriptBlockScoped()
        {
            RenameSymbolParams request = RenameVariableData.VariablewWithinHastableExpression;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableNestedFunctionScriptblock()
        {
            RenameSymbolParams request = RenameVariableData.VariableNestedFunctionScriptblock;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
                [Fact]
        public void VariableWithinCommandAstScriptBlock()
        {
            RenameSymbolParams request = RenameVariableData.VariableWithinCommandAstScriptBlock;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
                [Fact]
        public void VariableWithinForeachObject()
        {
            RenameSymbolParams request = RenameVariableData.VariableWithinForeachObject;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableusedInWhileLoop()
        {
            RenameSymbolParams request = RenameVariableData.VariableusedInWhileLoop;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
    }
}
