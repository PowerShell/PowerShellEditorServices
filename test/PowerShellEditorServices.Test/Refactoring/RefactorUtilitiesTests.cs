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
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Refactoring;
using System.Management.Automation.Language;
using System.Collections.Generic;
using System.Linq;

namespace PowerShellEditorServices.Test.Refactoring
{
    [Trait("Category", "RefactorUtilities")]
    public class RefactorUtilitiesTests : IDisposable
    {
        private readonly PsesInternalHost psesHost;
        private readonly WorkspaceService workspace;

        public RefactorUtilitiesTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
        }

        public void Dispose()
        {
#pragma warning disable VSTHRD002
            psesHost.StopAsync().Wait();
#pragma warning restore VSTHRD002
            GC.SuppressFinalize(this);
        }
        private ScriptFile GetTestScript(string fileName) => workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Refactoring\\Utilities", fileName)));

        [Fact]
        public void GetVariableExpressionAst()
        {
            RenameSymbolParams request = new(){
                Column=11,
                Line=15,
                RenameTo="Renamed",
                FileName="TestDetection.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line,request.Column,scriptFile.ScriptAst);
            Assert.Equal(15,symbol.Extent.StartLineNumber);
            Assert.Equal(1,symbol.Extent.StartColumnNumber);

        }
        [Fact]
        public void GetVariableExpressionStartAst()
        {
            RenameSymbolParams request = new(){
                Column=1,
                Line=15,
                RenameTo="Renamed",
                FileName="TestDetection.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line,request.Column,scriptFile.ScriptAst);
            Assert.Equal(15,symbol.Extent.StartLineNumber);
            Assert.Equal(1,symbol.Extent.StartColumnNumber);

        }
        [Fact]
        public void GetVariableWithinParameterAst()
        {
            RenameSymbolParams request = new(){
                Column=21,
                Line=3,
                RenameTo="Renamed",
                FileName="TestDetection.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line,request.Column,scriptFile.ScriptAst);
            Assert.Equal(3,symbol.Extent.StartLineNumber);
            Assert.Equal(17,symbol.Extent.StartColumnNumber);

        }
        [Fact]
        public void GetHashTableKey()
        {
            RenameSymbolParams request = new(){
                Column=9,
                Line=16,
                RenameTo="Renamed",
                FileName="TestDetection.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line,request.Column,scriptFile.ScriptAst);
            Assert.Equal(16,symbol.Extent.StartLineNumber);
            Assert.Equal(5,symbol.Extent.StartColumnNumber);

        }
        [Fact]
        public void GetVariableWithinCommandAst()
        {
            RenameSymbolParams request = new(){
                Column=29,
                Line=6,
                RenameTo="Renamed",
                FileName="TestDetection.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line,request.Column,scriptFile.ScriptAst);
            Assert.Equal(6,symbol.Extent.StartLineNumber);
            Assert.Equal(28,symbol.Extent.StartColumnNumber);

        }
        [Fact]
        public void GetCommandParameterAst()
        {
            RenameSymbolParams request = new(){
                Column=12,
                Line=21,
                RenameTo="Renamed",
                FileName="TestDetection.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line,request.Column,scriptFile.ScriptAst);
            Assert.Equal(21,symbol.Extent.StartLineNumber);
            Assert.Equal(10,symbol.Extent.StartColumnNumber);

        }
        [Fact]
        public void GetFunctionDefinitionAst()
        {
            RenameSymbolParams request = new(){
                Column=12,
                Line=1,
                RenameTo="Renamed",
                FileName="TestDetection.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line,request.Column,scriptFile.ScriptAst);
            Assert.Equal(1,symbol.Extent.StartLineNumber);
            Assert.Equal(1,symbol.Extent.StartColumnNumber);

        }
        [Fact]
        public void GetFunctionDefinitionAst()
        {
            RenameSymbolParams request = new()
            {
                Column = 16,
                Line = 1,
                RenameTo = "Renamed",
                FileName = "TestDetection.ps1"
            };
            ScriptFile scriptFile = GetTestScript(request.FileName);

            Ast symbol = Utilities.GetAst(request.Line, request.Column, scriptFile.ScriptAst);
            Assert.IsType<FunctionDefinitionAst>(symbol);
            Assert.Equal(1, symbol.Extent.StartLineNumber);
            Assert.Equal(1, symbol.Extent.StartColumnNumber);
        }
    }
}
