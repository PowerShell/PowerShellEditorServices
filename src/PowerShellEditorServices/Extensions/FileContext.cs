// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides context for a file that is open in the editor.
    /// </summary>
    public sealed class FileContext
    {
        #region Private Fields

        private readonly ScriptFile scriptFile;
        private readonly EditorContext editorContext;
        private readonly IEditorOperations editorOperations;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the parsed abstract syntax tree for the file.
        /// </summary>
        public Ast Ast => scriptFile.ScriptAst;

        /// <summary>
        /// Gets a BufferRange which represents the entire content
        /// range of the file.
        /// </summary>
        public IFileRange FileRange => new BufferFileRange(scriptFile.FileRange);

        /// <summary>
        /// Gets the language of the file.
        /// </summary>
        public string Language { get; }

        /// <summary>
        /// Gets the filesystem path of the file.
        /// </summary>
        public string Path => scriptFile.FilePath;

        /// <summary>
        /// Gets the URI of the file.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Gets the parsed token list for the file.
        /// </summary>
        public IReadOnlyList<Token> Tokens => scriptFile.ScriptTokens;

        /// <summary>
        /// Gets the workspace-relative path of the file.
        /// </summary>
        public string WorkspacePath => editorOperations.GetWorkspaceRelativePath(scriptFile);

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the FileContext class.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile to which this file refers.</param>
        /// <param name="editorContext">The EditorContext to which this file relates.</param>
        /// <param name="editorOperations">An IEditorOperations implementation which performs operations in the editor.</param>
        /// <param name="language">Determines the language of the file.false If it is not specified, then it defaults to "Unknown"</param>
        internal FileContext(
            ScriptFile scriptFile,
            EditorContext editorContext,
            IEditorOperations editorOperations,
            string language = "Unknown")
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                language = "Unknown";
            }

            this.scriptFile = scriptFile;
            this.editorContext = editorContext;
            this.editorOperations = editorOperations;
            Language = language;
            Uri = scriptFile.DocumentUri.ToUri();
        }

        #endregion

        #region Text Accessors

        /// <summary>
        /// Gets the complete file content as a string.
        /// </summary>
        /// <returns>A string containing the complete file content.</returns>
        public string GetText() => scriptFile.Contents;

        /// <summary>
        /// Gets the file content in the specified range as a string.
        /// </summary>
        /// <param name="bufferRange">The buffer range for which content will be extracted.</param>
        /// <returns>A string with the specified range of content.</returns>
        public string GetText(FileRange bufferRange)
        {
            return
                string.Join(
                    Environment.NewLine,
                    GetTextLines(bufferRange));
        }

        /// <summary>
        /// Gets the complete file content as an array of strings.
        /// </summary>
        /// <returns>An array of strings, each representing a line in the file.</returns>
        public string[] GetTextLines() => scriptFile.FileLines.ToArray();

        /// <summary>
        /// Gets the file content in the specified range as an array of strings.
        /// </summary>
        /// <param name="fileRange">The buffer range for which content will be extracted.</param>
        /// <returns>An array of strings, each representing a line in the file within the specified range.</returns>
        public string[] GetTextLines(FileRange fileRange) => scriptFile.GetLinesInRange(fileRange.ToBufferRange());

        #endregion

        #region Text Manipulation

        /// <summary>
        /// Inserts a text string at the current cursor position represented by
        /// the parent EditorContext's CursorPosition property.
        /// </summary>
        /// <param name="textToInsert">The text string to insert.</param>
        public void InsertText(string textToInsert)
        {
            // Is there a selection?
            if (editorContext.SelectedRange.HasRange())
            {
                InsertText(
                    textToInsert,
                    editorContext.SelectedRange);
            }
            else
            {
                InsertText(
                    textToInsert,
                    editorContext.CursorPosition);
            }
        }

        /// <summary>
        /// Inserts a text string at the specified buffer position.
        /// </summary>
        /// <param name="textToInsert">The text string to insert.</param>
        /// <param name="insertPosition">The position at which the text will be inserted.</param>
        public void InsertText(string textToInsert, IFilePosition insertPosition)
        {
            InsertText(
                textToInsert,
                new FileRange(insertPosition, insertPosition));
        }

        /// <summary>
        /// Inserts a text string at the specified line and column numbers.
        /// </summary>
        /// <param name="textToInsert">The text string to insert.</param>
        /// <param name="insertLine">The 1-based line number at which the text will be inserted.</param>
        /// <param name="insertColumn">The 1-based column number at which the text will be inserted.</param>
        public void InsertText(string textToInsert, int insertLine, int insertColumn)
        {
            InsertText(
                textToInsert,
                new FilePosition(insertLine, insertColumn));
        }

        /// <summary>
        /// Inserts a text string to replace the specified range, represented
        /// by starting and ending line and column numbers.  Can be used to
        /// insert, replace, or delete text depending on the specified range
        /// and text to insert.
        /// </summary>
        /// <param name="textToInsert">The text string to insert.</param>
        /// <param name="startLine">The 1-based starting line number where text will be replaced.</param>
        /// <param name="startColumn">The 1-based starting column number where text will be replaced.</param>
        /// <param name="endLine">The 1-based ending line number where text will be replaced.</param>
        /// <param name="endColumn">The 1-based ending column number where text will be replaced.</param>
        public void InsertText(
            string textToInsert,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn)
        {
            InsertText(
                textToInsert,
                new FileRange(
                    new FilePosition(startLine, startColumn),
                    new FilePosition(endLine, endColumn)));
        }

        /// <summary>
        /// Inserts a text string to replace the specified range. Can be
        /// used to insert, replace, or delete text depending on the specified
        /// range and text to insert.
        /// </summary>
        /// <param name="textToInsert">The text string to insert.</param>
        /// <param name="insertRange">The buffer range which will be replaced by the string.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void InsertText(string textToInsert, IFileRange insertRange)
        {
            editorOperations
                .InsertTextAsync(scriptFile.DocumentUri.ToString(), textToInsert, insertRange.ToBufferRange())
                .Wait();
        }

        #endregion

        #region File Manipulation

        /// <summary>
        /// Saves this file.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "Supporting synchronous API.")]
        public void Save() => editorOperations.SaveFileAsync(scriptFile.FilePath);

        /// <summary>
        /// Save this file under a new path and open a new editor window on that file.
        /// </summary>
        /// <param name="newFilePath">
        /// the path where the file should be saved,
        /// including the file name with extension as the leaf
        /// </param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void SaveAs(string newFilePath)
        {
            // Do some validation here so that we can provide a helpful error if the path won't work
            string absolutePath = System.IO.Path.IsPathRooted(newFilePath) ?
                newFilePath :
                System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(scriptFile.FilePath), newFilePath));

            if (File.Exists(absolutePath))
            {
                throw new IOException(string.Format("The file '{0}' already exists", absolutePath));
            }

            editorOperations.SaveFileAsync(scriptFile.FilePath, newFilePath).Wait();
        }

        #endregion
    }
}
