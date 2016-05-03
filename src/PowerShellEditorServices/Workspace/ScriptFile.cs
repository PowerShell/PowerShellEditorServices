﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Contains the details and contents of an open script file.
    /// </summary>
    public class ScriptFile
    {
        #region Private Fields

        private Token[] scriptTokens;
        private Version powerShellVersion;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a unique string that identifies this file.  At this time,
        /// this property returns a normalized version of the value stored
        /// in the FilePath property.
        /// </summary>
        public string Id
        {
            get { return this.FilePath.ToLower(); }
        }

        /// <summary>
        /// Gets the path at which this file resides.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets the path which the editor client uses to identify this file.
        /// </summary>
        public string ClientFilePath { get; private set; }

        /// <summary>
        /// Gets or sets a boolean that determines whether
        /// semantic analysis should be enabled for this file.
        /// For internal use only.
        /// </summary>
        internal bool IsAnalysisEnabled { get; set; }

        /// <summary>
        /// Gets a boolean that determines whether this file is
        /// in-memory or not (either unsaved or non-file content).
        /// </summary>
        public bool IsInMemory { get; private set; }

        /// <summary>
        /// Gets a string containing the full contents of the file.
        /// </summary>
        public string Contents 
        {
            get
            {
                return string.Join("\r\n", this.FileLines);
            }
        }

        /// <summary>
        /// Gets a BufferRange that represents the entire content
        /// range of the file.
        /// </summary>
        public BufferRange FileRange { get; private set; }

        /// <summary>
        /// Gets the list of syntax markers found by parsing this
        /// file's contents.
        /// </summary>
        public ScriptFileMarker[] SyntaxMarkers
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the list of strings for each line of the file.
        /// </summary>
        internal IList<string> FileLines
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the ScriptBlockAst representing the parsed script contents.
        /// </summary>
        public ScriptBlockAst ScriptAst
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the array of Tokens representing the parsed script contents.
        /// </summary>
        public Token[] ScriptTokens
        {
            get { return this.scriptTokens; }
        }

        /// <summary>
        /// Gets the array of filepaths dot sourced in this ScriptFile 
        /// </summary>
        public string[] ReferencedFiles
        {
            get;
            private set;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new ScriptFile instance by reading file contents from
        /// the given TextReader.
        /// </summary>
        /// <param name="filePath">The path at which the script file resides.</param>
        /// <param name="clientFilePath">The path which the client uses to identify the file.</param>
        /// <param name="textReader">The TextReader to use for reading the file's contents.</param>
        /// <param name="powerShellVersion">The version of PowerShell for which the script is being parsed.</param>
        public ScriptFile(
            string filePath,
            string clientFilePath,
            TextReader textReader,
            Version powerShellVersion)
        {
            this.FilePath = filePath;
            this.ClientFilePath = clientFilePath;
            this.IsAnalysisEnabled = true;
            this.IsInMemory = Workspace.IsPathInMemory(filePath);
            this.powerShellVersion = powerShellVersion;

            this.SetFileContents(textReader.ReadToEnd());
        }

        /// <summary>
        /// Creates a new ScriptFile instance with the specified file contents.
        /// </summary>
        /// <param name="filePath">The path at which the script file resides.</param>
        /// <param name="clientFilePath">The path which the client uses to identify the file.</param>
        /// <param name="initialBuffer">The initial contents of the script file.</param>
        /// <param name="powerShellVersion">The version of PowerShell for which the script is being parsed.</param>
        public ScriptFile(
            string filePath,
            string clientFilePath,
            string initialBuffer,
            Version powerShellVersion)
        {
            this.FilePath = filePath;
            this.ClientFilePath = clientFilePath;
            this.IsAnalysisEnabled = true;
            this.powerShellVersion = powerShellVersion;

            this.SetFileContents(initialBuffer);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a line from the file's contents.
        /// </summary>
        /// <param name="lineNumber">The 1-based line number in the file.</param>
        /// <returns>The complete line at the given line number.</returns>
        public string GetLine(int lineNumber)
        {
            Validate.IsWithinRange(
                "lineNumber", lineNumber,
                1, this.FileLines.Count + 1);

            return this.FileLines[lineNumber - 1];
        }

        /// <summary>
        /// Gets a range of lines from the file's contents.
        /// </summary>
        /// <param name="bufferRange">The buffer range from which lines will be extracted.</param>
        /// <returns>An array of strings from the specified range of the file.</returns>
        public string[] GetLinesInRange(BufferRange bufferRange)
        {
            this.ValidatePosition(bufferRange.Start);
            this.ValidatePosition(bufferRange.End);

            List<string> linesInRange = new List<string>();

            int startLine = bufferRange.Start.Line,
                endLine = bufferRange.End.Line;

            for (int line = startLine; line <= endLine; line++)
            {
                string currentLine = this.FileLines[line - 1];
                int startColumn =
                    line == startLine
                    ? bufferRange.Start.Column
                    : 1;
                int endColumn =
                    line == endLine
                    ? bufferRange.End.Column
                    : currentLine.Length + 1;

                currentLine =
                    currentLine.Substring(
                        startColumn - 1,
                        endColumn - startColumn);

                linesInRange.Add(currentLine);
            }

            return linesInRange.ToArray();
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the given position is outside
        /// of the file's buffer extents.
        /// </summary>
        /// <param name="bufferPosition">The position in the buffer to be validated.</param>
        public void ValidatePosition(BufferPosition bufferPosition)
        {
            this.ValidatePosition(
                bufferPosition.Line,
                bufferPosition.Column);
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the given position is outside
        /// of the file's buffer extents.
        /// </summary>
        /// <param name="line">The 1-based line to be validated.</param>
        /// <param name="column">The 1-based column to be validated.</param>
        public void ValidatePosition(int line, int column)
        {
            if (line < 1 || line > this.FileLines.Count + 1)
            {
                throw new ArgumentOutOfRangeException("Position is outside of file line range.");
            }

            // The maximum column is either one past the length of the string
            // or 1 if the string is empty.
            string lineString = this.FileLines[line - 1];
            int maxColumn = lineString.Length > 0 ? lineString.Length + 1 : 1;

            if (column < 1 || column > maxColumn)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(
                        "Position is outside of column range for line {0}.",
                        line));
            }
        }

        /// <summary>
        /// Applies the provided FileChange to the file's contents
        /// </summary>
        /// <param name="fileChange">The FileChange to apply to the file's contents.</param>
        public void ApplyChange(FileChange fileChange)
        {
            this.ValidatePosition(fileChange.Line, fileChange.Offset);
            this.ValidatePosition(fileChange.EndLine, fileChange.EndOffset);

            // Break up the change lines
            string[] changeLines = fileChange.InsertString.Split('\n');

            // Get the first fragment of the first line
            string firstLineFragment = 
                this.FileLines[fileChange.Line - 1]
                    .Substring(0, fileChange.Offset - 1);

            // Get the last fragment of the last line
            string endLine = this.FileLines[fileChange.EndLine - 1];
            string lastLineFragment = 
                endLine.Substring(
                    fileChange.EndOffset - 1, 
                    (this.FileLines[fileChange.EndLine - 1].Length - fileChange.EndOffset) + 1);

            // Remove the old lines
            for (int i = 0; i <= fileChange.EndLine - fileChange.Line; i++)
            {
                this.FileLines.RemoveAt(fileChange.Line - 1); 
            }

            // Build and insert the new lines
            int currentLineNumber = fileChange.Line;
            for (int changeIndex = 0; changeIndex < changeLines.Length; changeIndex++)
            {
                // Since we split the lines above using \n, make sure to
                // trim the ending \r's off as well.
                string finalLine = changeLines[changeIndex].TrimEnd('\r');

                // Should we add first or last line fragments?
                if (changeIndex == 0)
                {
                    // Append the first line fragment
                    finalLine = firstLineFragment + finalLine;
                }
                if (changeIndex == changeLines.Length - 1)
                {
                    // Append the last line fragment
                    finalLine = finalLine + lastLineFragment;
                }

                this.FileLines.Insert(currentLineNumber - 1, finalLine);
                currentLineNumber++;
            }

            // Parse the script again to be up-to-date
            this.ParseFileContents();
        }

        /// <summary>
        /// Calculates the zero-based character offset of a given
        /// line and column position in the file.
        /// </summary>
        /// <param name="lineNumber">The 1-based line number from which the offset is calculated.</param>
        /// <param name="columnNumber">The 1-based column number from which the offset is calculated.</param>
        /// <returns>The zero-based offset for the given file position.</returns>
        public int GetOffsetAtPosition(int lineNumber, int columnNumber)
        {
            Validate.IsWithinRange("lineNumber", lineNumber, 1, this.FileLines.Count);
            Validate.IsGreaterThan("columnNumber", columnNumber, 0);

            int offset = 0;

            for(int i = 0; i < lineNumber; i++)
            {
                if (i == lineNumber - 1)
                {
                    // Subtract 1 to account for 1-based column numbering
                    offset += columnNumber - 1; 
                }
                else
                {
                    // Add an offset to account for the current platform's newline characters
                    offset += this.FileLines[i].Length + Environment.NewLine.Length;
                }
            }

            return offset;
        }

        /// <summary>
        /// Calculates a FilePosition relative to a starting BufferPosition
        /// using the given 1-based line and column offset.
        /// </summary>
        /// <param name="originalPosition">The original BufferPosition from which an new position should be calculated.</param>
        /// <param name="lineOffset">The 1-based line offset added to the original position in this file.</param>
        /// <param name="columnOffset">The 1-based column offset added to the original position in this file.</param>
        /// <returns>A new FilePosition instance with the resulting line and column number.</returns>
        public FilePosition CalculatePosition(
            BufferPosition originalPosition,
            int lineOffset,
            int columnOffset)
        {
            int newLine = originalPosition.Line + lineOffset,
                newColumn = originalPosition.Column + columnOffset;

            this.ValidatePosition(newLine, newColumn);

            string scriptLine = this.FileLines[newLine - 1];
            newColumn = Math.Min(scriptLine.Length + 1, newColumn);

            return new FilePosition(this, newLine, newColumn);
        }

        /// <summary>
        /// Calculates the 1-based line and column number position based
        /// on the given buffer offset.
        /// </summary>
        /// <param name="bufferOffset">The buffer offset to convert.</param>
        /// <returns>A new BufferPosition containing the position of the offset.</returns>
        public BufferPosition GetPositionAtOffset(int bufferOffset)
        {
            BufferRange bufferRange = 
                GetRangeBetweenOffsets(
                    bufferOffset, bufferOffset);

            return bufferRange.Start;
        }

        /// <summary>
        /// Calculates the 1-based line and column number range based on
        /// the given start and end buffer offsets.
        /// </summary>
        /// <param name="startOffset">The start offset of the range.</param>
        /// <param name="endOffset">The end offset of the range.</param>
        /// <returns>A new BufferRange containing the positions in the offset range.</returns>
        public BufferRange GetRangeBetweenOffsets(int startOffset, int endOffset)
        {
            bool foundStart = false;
            int currentOffset = 0;
            int searchedOffset = startOffset;

            BufferPosition startPosition = new BufferPosition(0, 0);
            BufferPosition endPosition = startPosition;

            int line = 0;
            while (line < this.FileLines.Count)
            {
                if (searchedOffset <= currentOffset + this.FileLines[line].Length)
                {
                    int column = searchedOffset - currentOffset;

                    // Have we already found the start position?
                    if (foundStart)
                    {
                        // Assign the end position and end the search
                        endPosition = new BufferPosition(line + 1, column + 1);
                        break;
                    }
                    else
                    {
                        startPosition = new BufferPosition(line + 1, column + 1);

                        // Do we only need to find the start position?
                        if (startOffset == endOffset)
                        {
                            endPosition = startPosition;
                            break;
                        }
                        else
                        {
                            // Since the end offset can be on the same line,
                            // skip the line increment and continue searching
                            // for the end position
                            foundStart = true;
                            searchedOffset = endOffset;
                            continue;
                        }
                    }
                }

                // Increase the current offset and include newline length
                currentOffset += this.FileLines[line].Length + Environment.NewLine.Length;
                line++;
            }

            return new BufferRange(startPosition, endPosition);
        }

        #endregion

        #region Private Methods

        private void SetFileContents(string fileContents)
        {
            // Split the file contents into lines and trim
            // any carriage returns from the strings.
            this.FileLines =
                fileContents
                    .Split('\n')
                    .Select(line => line.TrimEnd('\r'))
                    .ToList();

            // Parse the contents to get syntax tree and errors
            this.ParseFileContents();
        }

        /// <summary>
        /// Parses the current file contents to get the AST, tokens,
        /// and parse errors.
        /// </summary>
        private void ParseFileContents()
        {
            ParseError[] parseErrors = null;

            // First, get the updated file range
            int lineCount = this.FileLines.Count;
            if (lineCount > 0)
            {
                this.FileRange =
                    new BufferRange(
                        new BufferPosition(1, 1),
                        new BufferPosition(
                            lineCount + 1,
                            this.FileLines[lineCount - 1].Length + 1));
            }
            else
            {
                this.FileRange = BufferRange.None;
            }

            try
            {
#if PowerShellv5r2
                // This overload appeared with Windows 10 Update 1
                if (this.powerShellVersion.Major >= 5 &&
                    this.powerShellVersion.Build >= 10586)
                {
                    // Include the file path so that module relative
                    // paths are evaluated correctly
                    this.ScriptAst =
                        Parser.ParseInput(
                            this.Contents,
                            this.FilePath,
                            out this.scriptTokens,
                            out parseErrors);
                }
                else
                {
                    this.ScriptAst =
                        Parser.ParseInput(
                            this.Contents,
                            out this.scriptTokens,
                            out parseErrors);
                }
#else
                this.ScriptAst =
                    Parser.ParseInput(
                        this.Contents,
                        out this.scriptTokens,
                        out parseErrors);
#endif
            }
            catch (RuntimeException ex)
            {
                var parseError =
                    new ParseError(
                        null,
                        ex.ErrorRecord.FullyQualifiedErrorId, 
                        ex.Message);

                parseErrors = new[] { parseError };
                this.scriptTokens = new Token[0];
                this.ScriptAst = null;
            }

            // Translate parse errors into syntax markers
            this.SyntaxMarkers =
                parseErrors
                    .Select(ScriptFileMarker.FromParseError)
                    .ToArray();
            
            //Get all dot sourced referenced files and store  them
            this.ReferencedFiles =
                AstOperations.FindDotSourcedIncludes(this.ScriptAst);
        }

#endregion
    }
}
