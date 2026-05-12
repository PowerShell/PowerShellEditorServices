// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Xunit;

namespace PowerShellEditorServices.Test.Extensions
{
    [Trait("Category", "Extensions")]
    public class EditorWorkspaceTests
    {
        [Fact]
        public void DocumentsReturnsOpenWorkspaceDocuments()
        {
            TestEditorOperations editorOperations = new()
            {
                OpenDocumentPaths = new[] { @"C:\test\one.ps1", @"C:\test\two.ps1" }
            };

            EditorWorkspace workspace = new(editorOperations);

            EditorWorkspaceDocument[] documents = workspace.Documents;

            Assert.Collection(
                documents,
                document => Assert.Equal(@"C:\test\one.ps1", document.Path),
                document => Assert.Equal(@"C:\test\two.ps1", document.Path));
        }

        [Fact]
        public void DocumentOpenSaveAndCloseUseWorkspaceOperations()
        {
            const string filePath = @"C:\test\file.ps1";
            TestEditorOperations editorOperations = new()
            {
                OpenDocumentPaths = new[] { filePath }
            };

            EditorWorkspace workspace = new(editorOperations);
            EditorWorkspaceDocument document = Assert.Single(workspace.Documents);

            document.Open();
            document.Save();
            document.Close();

            Assert.Collection(
                editorOperations.Calls,
                call => Assert.Equal("OpenFile:" + filePath, call),
                call => Assert.Equal("SaveFile:" + filePath, call),
                call => Assert.Equal("CloseFile:" + filePath, call));
        }

        private sealed class TestEditorOperations : IEditorOperations
        {
            public string[] OpenDocumentPaths { get; set; } = Array.Empty<string>();

            public List<string> Calls { get; } = new();

            public Task<EditorContext> GetEditorContextAsync() => Task.FromResult(default(EditorContext));

            public string GetWorkspacePath() => @"C:\test";

            public string[] GetWorkspacePaths() => new[] { @"C:\test" };

            public string[] GetWorkspaceOpenDocumentPaths() => OpenDocumentPaths;

            public string GetWorkspaceRelativePath(ScriptFile scriptFile) => scriptFile.FilePath;

            public Task NewFileAsync() => Task.CompletedTask;

            public Task NewFileAsync(string content) => Task.CompletedTask;

            public Task OpenFileAsync(string filePath)
            {
                Calls.Add("OpenFile:" + filePath);
                return Task.CompletedTask;
            }

            public Task OpenFileAsync(string filePath, bool preview) => Task.CompletedTask;

            public Task CloseFileAsync(string filePath)
            {
                Calls.Add("CloseFile:" + filePath);
                return Task.CompletedTask;
            }

            public Task SaveFileAsync(string filePath)
            {
                Calls.Add("SaveFile:" + filePath);
                return Task.CompletedTask;
            }

            public Task SaveFileAsync(string oldFilePath, string newFilePath) => Task.CompletedTask;

            public Task InsertTextAsync(string filePath, string insertText, BufferRange insertRange) => Task.CompletedTask;

            public Task SetSelectionAsync(BufferRange selectionRange) => Task.CompletedTask;

            public Task ShowInformationMessageAsync(string message) => Task.CompletedTask;

            public Task ShowErrorMessageAsync(string message) => Task.CompletedTask;

            public Task ShowWarningMessageAsync(string message) => Task.CompletedTask;

            public Task SetStatusBarMessageAsync(string message, int? timeout) => Task.CompletedTask;

            public void ClearTerminal()
            {
            }
        }
    }
}
