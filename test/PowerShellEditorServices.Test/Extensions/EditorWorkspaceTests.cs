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
                OpenDocuments = new[]
                {
                    new WorkspaceOpenDocument(@"C:\test\one.ps1", saved: true),
                    new WorkspaceOpenDocument(@"C:\test\two.ps1", saved: true)
                }
            };

            EditorWorkspace workspace = new(editorOperations);

            IEnumerable<EditorWorkspaceDocument> documents = workspace.Documents;

            Assert.Collection(
                documents,
                document =>
                {
                    Assert.Equal(@"C:\test\one.ps1", document.Path);
                    Assert.True(document.Saved);
                },
                document =>
                {
                    Assert.Equal(@"C:\test\two.ps1", document.Path);
                    Assert.True(document.Saved);
                });
        }

        [Fact]
        public void DocumentOpenSaveAndCloseUseWorkspaceOperations()
        {
            const string filePath = @"C:\test\file.ps1";
            TestEditorOperations editorOperations = new()
            {
                OpenDocuments = new[] { new WorkspaceOpenDocument(filePath, saved: true) }
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

        [Fact]
        public void DocumentToStringReturnsFileNameAndSavedStatus()
        {
            const string savedFilePath = @"C:\test\file.ps1";
            const string unsavedFilePath = @"C:\test\other.ps1";
            TestEditorOperations editorOperations = new()
            {
                OpenDocuments = new[]
                {
                    new WorkspaceOpenDocument(savedFilePath, saved: true),
                    new WorkspaceOpenDocument(unsavedFilePath, saved: false)
                }
            };

            EditorWorkspace workspace = new(editorOperations);
            IEnumerable<EditorWorkspaceDocument> documents = workspace.Documents;

            Assert.Collection(
                documents,
                document => Assert.Equal("file.ps1", document.ToString()),
                document => Assert.Equal("other.ps1 [Unsaved]", document.ToString()));
        }

        [Fact]
        public void DocumentSavedReturnsWorkspaceSavedState()
        {
            TestEditorOperations editorOperations = new()
            {
                OpenDocuments = new[]
                {
                    new WorkspaceOpenDocument(@"C:\test\saved.ps1", saved: true),
                    new WorkspaceOpenDocument(@"C:\test\unsaved.ps1", saved: false)
                }
            };

            EditorWorkspace workspace = new(editorOperations);
            IEnumerable<EditorWorkspaceDocument> documents = workspace.Documents;

            Assert.Collection(
                documents,
                document => Assert.True(document.Saved),
                document => Assert.False(document.Saved));
        }

        private sealed class TestEditorOperations : IEditorOperations
        {
            public WorkspaceOpenDocument[] OpenDocuments { get; set; } = Array.Empty<WorkspaceOpenDocument>();

            public List<string> Calls { get; } = new();

            public Task<EditorContext> GetEditorContextAsync() => Task.FromResult(default(EditorContext));

            public string GetWorkspacePath() => @"C:\test";

            public string[] GetWorkspacePaths() => new[] { @"C:\test" };

            public WorkspaceOpenDocument[] GetWorkspaceOpenDocuments() => OpenDocuments;

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
