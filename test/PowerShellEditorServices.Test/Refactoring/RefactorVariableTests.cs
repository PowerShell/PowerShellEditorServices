// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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

        internal static string TestRenaming(ScriptFile scriptFile, RenameSymbolParams request)
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

        [Fact]
        public void RefactorVariableSingle()
        {
            RenameSymbolParams request = RenameVariableData.SimpleVariableAssignment;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RefactorVariableNestedScopeFunction()
        {
            RenameSymbolParams request = RenameVariableData.VariableNestedScopeFunction;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RefactorVariableInPipeline()
        {
            RenameSymbolParams request = RenameVariableData.VariableInPipeline;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RefactorVariableInScriptBlock()
        {
            RenameSymbolParams request = RenameVariableData.VariableInScriptblock;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void RefactorVariableInScriptBlockScoped()
        {
            RenameSymbolParams request = RenameVariableData.VariablewWithinHastableExpression;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableNestedFunctionScriptblock()
        {
            RenameSymbolParams request = RenameVariableData.VariableNestedFunctionScriptblock;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableWithinCommandAstScriptBlock()
        {
            RenameSymbolParams request = RenameVariableData.VariableWithinCommandAstScriptBlock;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableWithinForeachObject()
        {
            RenameSymbolParams request = RenameVariableData.VariableWithinForeachObject;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableusedInWhileLoop()
        {
            RenameSymbolParams request = RenameVariableData.VariableusedInWhileLoop;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableInParam()
        {
            RenameSymbolParams request = RenameVariableData.VariableInParam;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableCommandParameter()
        {
            RenameSymbolParams request = RenameVariableData.VariableCommandParameter;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableCommandParameterReverse()
        {
            RenameSymbolParams request = RenameVariableData.VariableCommandParameterReverse;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableScriptWithParamBlock()
        {
            RenameSymbolParams request = RenameVariableData.VariableScriptWithParamBlock;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableNonParam()
        {
            RenameSymbolParams request = RenameVariableData.VariableNonParam;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);

        }
        [Fact]
        public void VariableParameterCommandWithSameName()
        {
            RenameSymbolParams request = RenameVariableData.VariableParameterCommandWithSameName;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void VarableCommandParameterSplattedFromCommandAst()
        {
            RenameSymbolParams request = RenameVariableData.VariableCommandParameterSplattedFromCommandAst;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        [Fact]
        public void VarableCommandParameterSplattedFromSplat()
        {
            RenameSymbolParams request = RenameVariableData.VariableCommandParameterSplattedFromSplat;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");

            string modifiedcontent = TestRenaming(scriptFile, request);

            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
    }
}
