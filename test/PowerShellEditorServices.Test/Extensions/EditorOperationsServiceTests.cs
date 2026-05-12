// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Extension;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

namespace PowerShellEditorServices.Test.Extensions
{
	[Trait("Category", "Extensions")]
	public class EditorOperationsServiceTests
	{
		[Fact]
		public void GetWorkspaceOpenDocumentsReturnsOnlyOpenDocumentsAndCurrentInMemoryState()
		{
			WorkspaceService workspaceService = new(NullLoggerFactory.Instance);

			ScriptFile openSaved = CreateFileBuffer(workspaceService, "open-saved.ps1");
			openSaved.IsOpen = true;
			openSaved.IsInMemory = false;

			ScriptFile openUnsaved = CreateFileBuffer(workspaceService, "open-unsaved.ps1");
			openUnsaved.IsOpen = true;
			openUnsaved.IsInMemory = true;

			ScriptFile closed = CreateFileBuffer(workspaceService, "closed.ps1");
			closed.IsOpen = false;
			closed.IsInMemory = false;

			EditorOperationsService editorOperationsService = new(
					psesHost: null,
					workspaceService,
					languageServer: null);

			WorkspaceOpenDocument[] documents = editorOperationsService.GetWorkspaceOpenDocuments();

			Assert.Equal(2, documents.Length);
			Assert.Contains(documents, static document => document.Path.EndsWith("open-saved.ps1") && document.Saved);
			Assert.Contains(documents, static document => document.Path.EndsWith("open-unsaved.ps1") && !document.Saved);
			Assert.DoesNotContain(documents, static document => document.Path.EndsWith("closed.ps1"));
		}

		private static ScriptFile CreateFileBuffer(WorkspaceService workspaceService, string fileName)
		{
			string filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), fileName);
			return workspaceService.GetFileBuffer(DocumentUri.FromFileSystemPath(filePath), initialBuffer: string.Empty);
		}
	}
}
