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
using PowerShellEditorServices.Test.Shared.Refactoring;
using Microsoft.PowerShell.EditorServices.Refactoring;

namespace PowerShellEditorServices.Test.Refactoring
{
    [Trait("Category", "RefactorFunction")]
    public class RefactorFunctionTests : IDisposable

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
        private ScriptFile GetTestScript(string fileName) => workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Refactoring", fileName)));

        internal static string GetModifiedScript(string OriginalScript, ModifiedFileResponse Modification)
        {

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

            //FunctionRename visitor = new(symbol.NameRegion.Text,
            //                            request.RenameTo,
            //                            symbol.ScriptRegion.StartLineNumber,
            //                            symbol.ScriptRegion.StartColumnNumber,
            //                            scriptFile.ScriptAst);
            //                            scriptFile.ScriptAst.Visit(visitor);
            IterativeFunctionRename iterative = new(symbol.NameRegion.Text,
                                        request.RenameTo,
                                        symbol.ScriptRegion.StartLineNumber,
                                        symbol.ScriptRegion.StartColumnNumber,
                                        scriptFile.ScriptAst);
            iterative.Visit(scriptFile.ScriptAst);
            //scriptFile.ScriptAst.Visit(visitor);
            ModifiedFileResponse changes = new(request.FileName)
            {
                Changes = iterative.Modifications
            };
            return GetModifiedScript(scriptFile.Contents, changes);
        }
        public RefactorFunctionTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
        }
        [Fact]
        public void RefactorFunctionSingle()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionsSingle;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RenameFunctionMultipleOccurrences()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionMultipleOccurrences;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RenameFunctionNested()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionInnerIsNested;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionOuterHasNestedFunction()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionOuterHasNestedFunction;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RenameFunctionInnerIsNested()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionInnerIsNested;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionWithInternalCalls()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionWithInternalCalls;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionCmdlet()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionCmdlet;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionSameName()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionSameName;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionInSscriptblock()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionScriptblock;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionInLoop()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionLoop;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionInForeach()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionForeach;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionInForeachObject()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionForeachObject;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line,
                    request.Column);
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void RenameFunctionCallWIthinStringExpression()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionCallWIthinStringExpression;
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
