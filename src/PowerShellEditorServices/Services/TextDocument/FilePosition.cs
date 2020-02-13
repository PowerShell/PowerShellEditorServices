//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    /// <summary>
    /// Provides details and operations for a buffer position in a
    /// specific file.
    /// </summary>
    internal sealed class FilePosition : BufferPosition
    {
        #region Private Fields

        private ScriptFile scriptFile;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new FilePosition instance for the 1-based line and
        /// column numbers in the specified file.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the position is located.</param>
        /// <param name="line">The 1-based line number in the file.</param>
        /// <param name="column">The 1-based column number in the file.</param>
        public FilePosition(
            ScriptFile scriptFile,
            int line,
            int column)
                : base(line, column)
        {
            this.scriptFile = scriptFile;
        }

        /// <summary>
        /// Creates a new FilePosition instance for the specified file by
        /// copying the specified BufferPosition
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the position is located.</param>
        /// <param name="copiedPosition">The original BufferPosition from which the line and column will be copied.</param>
        public FilePosition(
            ScriptFile scriptFile,
            BufferPosition copiedPosition)
                 : this(scriptFile, copiedPosition.Line, copiedPosition.Column)
        {
            scriptFile.ValidatePosition(copiedPosition);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a FilePosition relative to this position by adding the
        /// provided line and column offset relative to the contents of
        /// the current file.
        /// </summary>
        /// <param name="lineOffset">The line offset to add to this position.</param>
        /// <param name="columnOffset">The column offset to add to this position.</param>
        /// <returns>A new FilePosition instance for the calculated position.</returns>
        public FilePosition AddOffset(int lineOffset, int columnOffset)
        {
            return this.scriptFile.CalculatePosition(
                this,
                lineOffset,
                columnOffset);
        }

        /// <summary>
        /// Gets a FilePosition for the line and column position
        /// of the beginning of the current line after any initial
        /// whitespace for indentation.
        /// </summary>
        /// <returns>A new FilePosition instance for the calculated position.</returns>
        public FilePosition GetLineStart()
        {
            string scriptLine = scriptFile.FileLines[this.Line - 1];

            int lineStartColumn = 1;
            for (int i = 0; i < scriptLine.Length; i++)
            {
                if (!char.IsWhiteSpace(scriptLine[i]))
                {
                    lineStartColumn = i + 1;
                    break;
                }
            }

            return new FilePosition(this.scriptFile, this.Line, lineStartColumn);
        }

        /// <summary>
        /// Gets a FilePosition for the line and column position
        /// of the end of the current line.
        /// </summary>
        /// <returns>A new FilePosition instance for the calculated position.</returns>
        public FilePosition GetLineEnd()
        {
            string scriptLine = scriptFile.FileLines[this.Line - 1];
            return new FilePosition(this.scriptFile, this.Line, scriptLine.Length + 1);
        }

        #endregion
    }
}

