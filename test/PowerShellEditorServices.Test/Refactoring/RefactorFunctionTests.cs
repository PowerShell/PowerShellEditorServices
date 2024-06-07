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
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Refactoring;
using PowerShellEditorServices.Test.Shared.Refactoring.Functions;
using Xunit.Abstractions;
using MediatR;

namespace PowerShellEditorServices.Test.Refactoring
{

    [Trait("Category", "RefactorFunction")]
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

        internal static string TestRenaming(ScriptFile scriptFile, RenameSymbolParamsSerialized request, SymbolReference symbol)
        {
            IterativeFunctionRename iterative = new(symbol.NameRegion.Text,
                                        request.RenameTo,
                                        symbol.ScriptRegion.StartLineNumber,
                                        symbol.ScriptRegion.StartColumnNumber,
                                        scriptFile.ScriptAst);
            iterative.Visit(scriptFile.ScriptAst);
            ModifiedFileResponse changes = new(request.FileName)
            {
                Changes = iterative.Modifications
            };
            return GetModifiedScript(scriptFile.Contents, changes);
        }

        public class RenameSymbolParamsSerialized : IRequest<RenameSymbolResult>, IXunitSerializable
        {
            public string FileName { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
            public string RenameTo { get; set; }

            // Default constructor needed for deserialization
            public RenameSymbolParamsSerialized() { }

            // Parameterized constructor for convenience
            public RenameSymbolParamsSerialized(RenameSymbolParams RenameSymbolParams)
            {
                FileName = RenameSymbolParams.FileName;
                Line = RenameSymbolParams.Line;
                Column = RenameSymbolParams.Column;
                RenameTo = RenameSymbolParams.RenameTo;
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                FileName = info.GetValue<string>("FileName");
                Line = info.GetValue<int>("Line");
                Column = info.GetValue<int>("Column");
                RenameTo = info.GetValue<string>("RenameTo");
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue("FileName", FileName);
                info.AddValue("Line", Line);
                info.AddValue("Column", Column);
                info.AddValue("RenameTo", RenameTo);
            }

            public override string ToString() => $"{FileName}";
        }


        public class SimpleData : TheoryData<RenameSymbolParamsSerialized>
        {
            public SimpleData()
            {

                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionsSingle));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionWithInternalCalls));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionCmdlet));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionScriptblock));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionCallWIthinStringExpression));
            }

        }

        [Theory]
        [ClassData(typeof(SimpleData))]
        public void Simple(RenameSymbolParamsSerialized s)
        {
            // Arrange
            RenameSymbolParamsSerialized request = s;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
             request.Line,
             request.Column);
            // Act
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            // Assert
            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }

        public class MultiOccurrenceData : TheoryData<RenameSymbolParamsSerialized>
        {
            public MultiOccurrenceData()
            {
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionMultipleOccurrences));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionSameName));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionNestedRedefinition));
            }

        }

        [Theory]
        [ClassData(typeof(MultiOccurrenceData))]
        public void MultiOccurrence(RenameSymbolParamsSerialized s)
        {
            // Arrange
            RenameSymbolParamsSerialized request = s;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
             request.Line,
             request.Column);
            // Act
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            // Assert
            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }

        public class NestedData : TheoryData<RenameSymbolParamsSerialized>
        {
            public NestedData()
            {
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionInnerIsNested));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionOuterHasNestedFunction));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionInnerIsNested));
            }

        }

        [Theory]
        [ClassData(typeof(NestedData))]
        public void Nested(RenameSymbolParamsSerialized s)
        {
            // Arrange
            RenameSymbolParamsSerialized request = s;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
             request.Line,
             request.Column);
            // Act
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            // Assert
            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
        public class LoopsData : TheoryData<RenameSymbolParamsSerialized>
        {
            public LoopsData()
            {
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionLoop));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionForeach));
                Add(new RenameSymbolParamsSerialized(RefactorsFunctionData.FunctionForeachObject));
            }

        }

        [Theory]
        [ClassData(typeof(LoopsData))]
        public void Loops(RenameSymbolParamsSerialized s)
        {
            // Arrange
            RenameSymbolParamsSerialized request = s;
            ScriptFile scriptFile = GetTestScript(request.FileName);
            ScriptFile expectedContent = GetTestScript(request.FileName.Substring(0, request.FileName.Length - 4) + "Renamed.ps1");
            SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
             request.Line,
             request.Column);
            // Act
            string modifiedcontent = TestRenaming(scriptFile, request, symbol);

            // Assert
            Assert.Equal(expectedContent.Contents, modifiedcontent);
        }
    }
}
