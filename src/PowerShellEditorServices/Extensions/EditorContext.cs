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
    /// Provides context for the host editor at the time of creation.
    /// </summary>
    public class EditorContext
    {
        #region Private Fields

        private IEditorOperations editorOperations;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the FileContext for the active file.
        /// </summary>
        public FileContext CurrentFile { get; private set; }

        /// <summary>
        /// Gets the BufferRange representing the current selection in the file.
        /// </summary>
        public BufferRange SelectedRange { get; private set; }

        /// <summary>
        /// Gets the FilePosition representing the current cursor position.
        /// </summary>
        public FilePosition CursorPosition { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the EditorContext class.
        /// </summary>
        /// <param name="editorOperations">An IEditorOperations implementation which performs operations in the editor.</param>
        /// <param name="currentFile">The ScriptFile that is in the active editor buffer.</param>
        /// <param name="cursorPosition">The position of the user's cursor in the active editor buffer.</param>
        /// <param name="selectedRange">The range of the user's selection in the active editor buffer.</param>
        /// <param name="language">Determines the language of the file.false If it is not specified, then it defaults to "Unknown"</param>
        public EditorContext(
            IEditorOperations editorOperations,
            ScriptFile currentFile,
            BufferPosition cursorPosition,
            BufferRange selectedRange,
            string language = "Unknown")
        {
            this.editorOperations = editorOperations;
            this.CurrentFile = new FileContext(currentFile, this, editorOperations, language);
            this.SelectedRange = selectedRange;
            this.CursorPosition = new FilePosition(currentFile, cursorPosition);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets a selection in the host editor's active buffer.
        /// </summary>
        /// <param name="startLine">The 1-based starting line of the selection.</param>
        /// <param name="startColumn">The 1-based starting column of the selection.</param>
        /// <param name="endLine">The 1-based ending line of the selection.</param>
        /// <param name="endColumn">The 1-based ending column of the selection.</param>
        public void SetSelection(
            int startLine,
            int startColumn,
            int endLine,
            int endColumn)
        {
            this.SetSelection(
                new BufferRange(
                    startLine, startColumn,
                    endLine, endColumn));
        }

        /// <summary>
        /// Sets a selection in the host editor's active buffer.
        /// </summary>
        /// <param name="startPosition">The starting position of the selection.</param>
        /// <param name="endPosition">The ending position of the selection.</param>
        public void SetSelection(
            BufferPosition startPosition,
            BufferPosition endPosition)
        {
            this.SetSelection(
                new BufferRange(
                    startPosition,
                    endPosition));
        }

        /// <summary>
        /// Sets a selection in the host editor's active buffer.
        /// </summary>
        /// <param name="selectionRange">The range of the selection.</param>
        public void SetSelection(BufferRange selectionRange)
        {
            this.editorOperations
                .SetSelectionAsync(selectionRange)
                .Wait();
        }

        #endregion
    }
}

