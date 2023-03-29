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
        public RefactorFunctionTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
        }
        [Fact]
        public void RefactorFunctionSingle()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionsSingleParams;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line + 1,
                    request.Column + 1);
            ModifiedFileResponse changes = RenameSymbolHandler.RefactorFunction(symbol, scriptFile.ScriptAst, request);
            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 9 &&
                        item.EndColumn == 23 &&
                        item.StartLine == 0 &&
                        item.EndLine == 0 &&
                        request.RenameTo == item.NewText;
            });
            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 0 &&
                            item.EndColumn == 14 &&
                            item.StartLine == 4 &&
                            item.EndLine == 4 &&
                            request.RenameTo == item.NewText;
            });
        }
        [Fact]
        public void RefactorMultipleFromCommandDef()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionsMultipleFromCommandDef;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line + 1,
                    request.Column + 1);
            ModifiedFileResponse changes = RenameSymbolHandler.RefactorFunction(symbol, scriptFile.ScriptAst, request);
            Assert.Equal(3, changes.Changes.Count);

            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 9 &&
                        item.EndColumn == 12 &&
                        item.StartLine == 0 &&
                        item.EndLine == 0 &&
                        request.RenameTo == item.NewText;
            });
            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 4 &&
                        item.EndColumn == 7 &&
                        item.StartLine == 5 &&
                        item.EndLine == 5 &&
                        request.RenameTo == item.NewText;
            });
            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 4 &&
                        item.EndColumn == 7 &&
                        item.StartLine == 15 &&
                        item.EndLine == 15 &&
                        request.RenameTo == item.NewText;
            });
        }
        [Fact]
        public void RefactorNestedFunction()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionsMultiple;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line + 1,
                    request.Column + 1);
            ModifiedFileResponse changes = RenameSymbolHandler.RefactorFunction(symbol, scriptFile.ScriptAst, request);
            Assert.Equal(2, changes.Changes.Count);

            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 13 &&
                        item.EndColumn == 16 &&
                        item.StartLine == 4 &&
                        item.EndLine == 4 &&
                        request.RenameTo == item.NewText;
            });
            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 4 &&
                        item.EndColumn == 10 &&
                        item.StartLine == 6 &&
                        item.EndLine == 6 &&
                        request.RenameTo == item.NewText;
            });
        }
        [Fact]
        public void RefactorFlatFunction()
        {
            RenameSymbolParams request = RefactorsFunctionData.FunctionsSimpleFlat;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line + 1,
                    request.Column + 1);
            ModifiedFileResponse changes = RenameSymbolHandler.RefactorFunction(symbol, scriptFile.ScriptAst, request);
            Assert.Equal(2, changes.Changes.Count);

            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 47 &&
                        item.EndColumn == 50 &&
                        item.StartLine == 0 &&
                        item.EndLine == 0 &&
                        request.RenameTo == item.NewText;
            });
            Assert.Contains(changes.Changes, item =>
            {
                return item.StartColumn == 81 &&
                        item.EndColumn == 84 &&
                        item.StartLine == 0 &&
                        item.EndLine == 0 &&
                        request.RenameTo == item.NewText;
            });
        }
    }
}
