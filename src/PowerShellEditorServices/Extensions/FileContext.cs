//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides context for a file that is open in the editor.
    /// </summary>
    public class FileContext
    {
        #region Private Fields

        internal ScriptFile scriptFile;
        private EditorContext editorContext;
        private IEditorOperations editorOperations;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the filesystem path of the file.
        /// </summary>
        public string Path
        {
            get { return this.scriptFile.FilePath; }
        }

        /// <summary>
        /// Gets the workspace-relative path of the file.
        /// </summary>
        public string WorkspacePath
        {
            get
            {
                return
                    this.editorOperations.GetWorkspaceRelativePath(
                        this.scriptFile.FilePath);
            }
        }

        /// <summary>
        /// Gets the parsed abstract syntax tree for the file.
        /// </summary>
        public Ast Ast
        {
            get { return this.scriptFile.ScriptAst; }
        }

        /// <summary>
        /// Gets the parsed token list for the file.
        /// </summary>
        public Token[] Tokens
        {
            get { return this.scriptFile.ScriptTokens; }
        }

        /// <summary>
        /// Gets a BufferRange which represents the entire content
        /// range of the file.
        /// </summary>
        public BufferRange FileRange
        {
            get { return this.scriptFile.FileRange; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the FileContext class.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile to which this file refers.</param>
        /// <param name="editorContext">The EditorContext to which this file relates.</param>
        /// <param name="editorOperations">An IEditorOperations implementation which performs operations in the editor.</param>
        public FileContext(
            ScriptFile scriptFile,
            EditorContext editorContext,
            IEditorOperations editorOperations)
        {
            this.scriptFile = scriptFile;
            this.editorContext = editorContext;
            this.editorOperations = editorOperations;
        }

        #endregion

        #region Text Accessors

        /// <summary>
        /// Gets the complete file content as a string.
        /// </summary>
        /// <returns>A string containing the complete file content.</returns>
        public string GetText()
        {
            return this.scriptFile.Contents;
        }

        /// <summary>
        /// Gets the file content in the specified range as a string.
        /// </summary>
        /// <param name="bufferRange">The buffer range for which content will be extracted.</param>
        /// <returns>A string with the specified range of content.</returns>
        public string GetText(BufferRange bufferRange)
        {
            return
                string.Join(
                    Environment.NewLine,
                    this.GetTextLines(bufferRange));
        }

        /// <summary>
        /// Gets the complete file content as an array of strings.
        /// </summary>
        /// <returns>An array of strings, each representing a line in the file.</returns>
        public string[] GetTextLines()
        {
            return this.scriptFile.FileLines.ToArray();
        }

        /// <summary>
        /// Gets the file content in the specified range as an array of strings.
        /// </summary>
        /// <param name="bufferRange">The buffer range for which content will be extracted.</param>
        /// <returns>An array of strings, each representing a line in the file within the specified range.</returns>
        public string[] GetTextLines(BufferRange bufferRange)
        {
            return this.scriptFile.GetLinesInRange(bufferRange);
        }

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
            if (this.editorContext.SelectedRange.HasRange)
            {
                this.InsertText(
                    textToInsert,
                    this.editorContext.SelectedRange);
            }
            else
            {
                this.InsertText(
                    textToInsert,
                    this.editorContext.CursorPosition);
            }
        }

        /// <summary>
        /// Inserts a text string at the specified buffer position.
        /// </summary>
        /// <param name="textToInsert">The text string to insert.</param>
        /// <param name="insertPosition">The position at which the text will be inserted.</param>
        public void InsertText(string textToInsert, BufferPosition insertPosition)
        {
            this.InsertText(
                textToInsert,
                new BufferRange(insertPosition, insertPosition));
        }

        /// <summary>
        /// Inserts a text string at the specified line and column numbers.
        /// </summary>
        /// <param name="textToInsert">The text string to insert.</param>
        /// <param name="insertLine">The 1-based line number at which the text will be inserted.</param>
        /// <param name="insertColumn">The 1-based column number at which the text will be inserted.</param>
        public void InsertText(string textToInsert, int insertLine, int insertColumn)
        {
            this.InsertText(
                textToInsert,
                new BufferPosition(insertLine, insertColumn));
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
            this.InsertText(
                textToInsert,
                new BufferRange(
                    startLine,
                    startColumn,
                    endLine,
                    endColumn));
        }

        /// <summary>
        /// Inserts a text string to replace the specified range. Can be
        /// used to insert, replace, or delete text depending on the specified
        /// range and text to insert.
        /// </summary>
        /// <param name="textToInsert">The text string to insert.</param>
        /// <param name="insertRange">The buffer range which will be replaced by the string.</param>
        public void InsertText(string textToInsert, BufferRange insertRange)
        {
            this.editorOperations
                .InsertText(this.scriptFile.ClientFilePath, textToInsert, insertRange)
                .Wait();
        }

        #endregion

        #region File Manipulation

        /// <summary>
        /// Saves this file.
        /// </summary>
        public void Save()
        {
            this.editorOperations.SaveFile(this.scriptFile.FilePath);
        }

        #endregion
    }
}

