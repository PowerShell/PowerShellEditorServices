//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Extensions.Services
{
    /// <summary>
    /// A script file in the current editor workspace.
    /// </summary>
    public interface IEditorScriptFile
    {
        /// <summary>
        /// The URI of the script file.
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// The text content of the file.
        /// </summary>
        string Content { get; }

        /// <summary>
        /// The lines of the file.
        /// </summary>
        IReadOnlyList<string> Lines { get; }

        /// <summary>
        /// The PowerShell AST of the script in the file.
        /// </summary>
        ScriptBlockAst Ast { get; }

        /// <summary>
        /// The PowerShell syntactic tokens of the script in the file.
        /// </summary>
        IReadOnlyList<Token> Tokens { get; }
    }

    /// <summary>
    /// A service for querying and manipulating the editor workspace.
    /// </summary>
    public interface IWorkspaceService
    {
        /// <summary>
        /// The root path of the workspace.
        /// </summary>
        string WorkspacePath { get; }

        /// <summary>
        /// Indicates whether the editor is configured to follow symlinks.
        /// </summary>
        bool FollowSymlinks { get; }

        /// <summary>
        /// The list of file globs to exclude from workspace management.
        /// </summary>
        IReadOnlyList<string> ExcludedFileGlobs { get; }

        /// <summary>
        /// Get a file within the workspace.
        /// </summary>
        /// <param name="fileUri">The absolute URI of the file to get.</param>
        /// <returns>A representation of the file.</returns>
        IEditorScriptFile GetFile(Uri fileUri);

        /// <summary>
        /// Attempt to get a file within the workspace.
        /// </summary>
        /// <param name="fileUri">The absolute URI of the file to get.</param>
        /// <param name="file">The file, if it was found.</param>
        /// <returns>True if the file was found, false otherwise.</returns>
        bool TryGetFile(Uri fileUri, out IEditorScriptFile file);

        /// <summary>
        /// Get all the open files in the editor workspace.
        /// The result is not kept up to date as files are opened or closed.
        /// </summary>
        /// <returns>All open files in the editor workspace.</returns>
        IReadOnlyList<IEditorScriptFile> GetOpenedFiles();
    }

    internal class EditorScriptFile : IEditorScriptFile
    {
        private readonly ScriptFile _scriptFile;

        internal EditorScriptFile(
            ScriptFile scriptFile)
        {
            _scriptFile = scriptFile;
            Uri = scriptFile.DocumentUri.ToUri();
            Lines = _scriptFile.FileLines.AsReadOnly();
        }

        public Uri Uri { get; }

        public IReadOnlyList<string> Lines { get; }

        public string Content => _scriptFile.Contents;

        public ScriptBlockAst Ast => _scriptFile.ScriptAst;

        public IReadOnlyList<Token> Tokens => _scriptFile.ScriptTokens;
    }

    internal class WorkspaceService : IWorkspaceService
    {
        private readonly EditorServices.Services.WorkspaceService _workspaceService;

        internal WorkspaceService(
            EditorServices.Services.WorkspaceService workspaceService)
        {
            _workspaceService = workspaceService;
            ExcludedFileGlobs = _workspaceService.ExcludeFilesGlob.AsReadOnly();
        }

        public string WorkspacePath => _workspaceService.WorkspacePath;

        public bool FollowSymlinks => _workspaceService.FollowSymlinks;

        public IReadOnlyList<string> ExcludedFileGlobs { get; }

        public IEditorScriptFile GetFile(Uri fileUri) => GetEditorFileFromScriptFile(_workspaceService.GetFile(fileUri));

        public bool TryGetFile(Uri fileUri, out IEditorScriptFile file)
        {
            if (!_workspaceService.TryGetFile(fileUri.LocalPath, out ScriptFile scriptFile))
            {
                file = null;
                return false;
            }

            file = GetEditorFileFromScriptFile(scriptFile);
            return true;
        }

        public IReadOnlyList<IEditorScriptFile> GetOpenedFiles()
        {
            var files = new List<IEditorScriptFile>();
            foreach (ScriptFile openedFile in _workspaceService.GetOpenedFiles())
            {
                files.Add(GetEditorFileFromScriptFile(openedFile));
            }
            return files.AsReadOnly();
        }

        private IEditorScriptFile GetEditorFileFromScriptFile(ScriptFile file)
        {
            return new EditorScriptFile(file);
        }
    }
}
