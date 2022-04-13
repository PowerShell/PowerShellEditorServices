// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    /// <summary>
    /// Contains the details and contents of an open script file.
    /// </summary>
    internal sealed class ScriptFile
    {
        #region Private Fields

        private static readonly string[] s_newlines = new[]
        {
            "\r\n",
            "\n"
        };

        private readonly Version powerShellVersion;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a unique string that identifies this file.  At this time,
        /// this property returns a normalized version of the value stored
        /// in the FilePath property.
        /// </summary>
        public string Id => FilePath.ToLower();

        /// <summary>
        /// Gets the path at which this file resides.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the file path in LSP DocumentUri form.  The ClientPath property must not be null.
        /// </summary>
        public DocumentUri DocumentUri { get; set; }

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
        public bool IsInMemory { get; }

        /// <summary>
        /// Gets a string containing the full contents of the file.
        /// </summary>
        public string Contents => string.Join(Environment.NewLine, FileLines);

        /// <summary>
        /// Gets a BufferRange that represents the entire content
        /// range of the file.
        /// </summary>
        public BufferRange FileRange { get; private set; }

        /// <summary>
        /// Gets the list of syntax markers found by parsing this
        /// file's contents.
        /// </summary>
        public List<ScriptFileMarker> DiagnosticMarkers
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the list of strings for each line of the file.
        /// </summary>
        internal List<string> FileLines
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
            get;
            private set;
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
        /// <param name="docUri">The System.Uri of the file.</param>
        /// <param name="textReader">The TextReader to use for reading the file's contents.</param>
        /// <param name="powerShellVersion">The version of PowerShell for which the script is being parsed.</param>
        internal ScriptFile(
            DocumentUri docUri,
            TextReader textReader,
            Version powerShellVersion)
        {
            // For non-files, use their URI representation instead
            // so that other operations know it's untitled/in-memory
            // and don't think that it's a relative path
            // on the file system.
            IsInMemory = !docUri.ToUri().IsFile;
            FilePath = IsInMemory
                ? docUri.ToString()
                : docUri.GetFileSystemPath();
            DocumentUri = docUri;
            IsAnalysisEnabled = true;
            this.powerShellVersion = powerShellVersion;

            // SetFileContents() calls ParseFileContents() which initializes the rest of the properties.
            SetFileContents(textReader.ReadToEnd());
        }

        /// <summary>
        /// Creates a new ScriptFile instance with the specified file contents.
        /// </summary>
        /// <param name="fileUri">The System.Uri of the file.</param>
        /// <param name="initialBuffer">The initial contents of the script file.</param>
        /// <param name="powerShellVersion">The version of PowerShell for which the script is being parsed.</param>
        internal ScriptFile(
            DocumentUri fileUri,
            string initialBuffer,
            Version powerShellVersion)
            : this(
                  fileUri,
                  new StringReader(initialBuffer),
                  powerShellVersion)
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Get the lines in a string.
        /// </summary>
        /// <param name="text">Input string to be split up into lines.</param>
        /// <returns>The lines in the string.</returns>
        internal static IList<string> GetLines(string text) => GetLinesInternal(text);

        /// <summary>
        /// Get the lines in a string.
        /// </summary>
        /// <param name="text">Input string to be split up into lines.</param>
        /// <returns>The lines in the string.</returns>
        internal static List<string> GetLinesInternal(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new List<string>(text.Split(s_newlines, StringSplitOptions.None));
        }

        /// <summary>
        /// Deterines whether the supplied path indicates the file is an "untitled:Unitled-X"
        /// which has not been saved to file.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the path is an untitled file, false otherwise.</returns>
        internal static bool IsUntitledPath(string path)
        {
            Validate.IsNotNull(nameof(path), path);
            return !string.Equals(
                DocumentUri.From(path).Scheme,
                Uri.UriSchemeFile,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a line from the file's contents.
        /// </summary>
        /// <param name="lineNumber">The 1-based line number in the file.</param>
        /// <returns>The complete line at the given line number.</returns>
        public string GetLine(int lineNumber)
        {
            Validate.IsWithinRange(
                nameof(lineNumber), lineNumber,
                1, FileLines.Count + 1);

            return FileLines[lineNumber - 1];
        }

        /// <summary>
        /// Gets a range of lines from the file's contents.
        /// </summary>
        /// <param name="bufferRange">The buffer range from which lines will be extracted.</param>
        /// <returns>An array of strings from the specified range of the file.</returns>
        public string[] GetLinesInRange(BufferRange bufferRange)
        {
            ValidatePosition(bufferRange.Start);
            ValidatePosition(bufferRange.End);

            List<string> linesInRange = new();

            int startLine = bufferRange.Start.Line,
                endLine = bufferRange.End.Line;

            for (int line = startLine; line <= endLine; line++)
            {
                string currentLine = FileLines[line - 1];
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
            ValidatePosition(
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
            int maxLine = FileLines.Count;
            if (line < 1 || line > maxLine)
            {
                throw new ArgumentOutOfRangeException($"Position {line}:{column} is outside of the line range of 1 to {maxLine}.");
            }

            // The maximum column is either **one past** the length of the string
            // or 1 if the string is empty.
            string lineString = FileLines[line - 1];
            int maxColumn = lineString.Length > 0 ? lineString.Length + 1 : 1;

            if (column < 1 || column > maxColumn)
            {
                throw new ArgumentOutOfRangeException($"Position {line}:{column} is outside of the column range of 1 to {maxColumn}.");
            }
        }

        /// <summary>
        /// Applies the provided FileChange to the file's contents
        /// </summary>
        /// <param name="fileChange">The FileChange to apply to the file's contents.</param>
        public void ApplyChange(FileChange fileChange)
        {
            // Break up the change lines
            string[] changeLines = fileChange.InsertString.Split('\n');

            if (fileChange.IsReload)
            {
                FileLines.Clear();
                FileLines.AddRange(changeLines);
            }
            else
            {
                // VSCode sometimes likes to give the change start line as (FileLines.Count + 1).
                // This used to crash EditorServices, but we now treat it as an append.
                // See https://github.com/PowerShell/vscode-powershell/issues/1283
                if (fileChange.Line == FileLines.Count + 1)
                {
                    foreach (string addedLine in changeLines)
                    {
                        string finalLine = addedLine.TrimEnd('\r');
                        FileLines.Add(finalLine);
                    }
                }
                // Similarly, when lines are deleted from the end of the file,
                // VSCode likes to give the end line as (FileLines.Count + 1).
                else if (fileChange.EndLine == FileLines.Count + 1 && string.Empty.Equals(fileChange.InsertString))
                {
                    int lineIndex = fileChange.Line - 1;
                    FileLines.RemoveRange(lineIndex, FileLines.Count - lineIndex);
                }
                // Otherwise, the change needs to go between existing content
                else
                {
                    ValidatePosition(fileChange.Line, fileChange.Offset);
                    ValidatePosition(fileChange.EndLine, fileChange.EndOffset);

                    // Get the first fragment of the first line
                    string firstLineFragment =
                    FileLines[fileChange.Line - 1]
                        .Substring(0, fileChange.Offset - 1);

                    // Get the last fragment of the last line
                    string endLine = FileLines[fileChange.EndLine - 1];
                    string lastLineFragment =
                    endLine.Substring(
                        fileChange.EndOffset - 1,
                        FileLines[fileChange.EndLine - 1].Length - fileChange.EndOffset + 1);

                    // Remove the old lines
                    for (int i = 0; i <= fileChange.EndLine - fileChange.Line; i++)
                    {
                        FileLines.RemoveAt(fileChange.Line - 1);
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
                            finalLine += lastLineFragment;
                        }

                        FileLines.Insert(currentLineNumber - 1, finalLine);
                        currentLineNumber++;
                    }
                }
            }

            // Parse the script again to be up-to-date
            ParseFileContents();
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
            Validate.IsWithinRange(nameof(lineNumber), lineNumber, 1, FileLines.Count + 1);
            Validate.IsGreaterThan(nameof(columnNumber), columnNumber, 0);

            int offset = 0;

            for (int i = 0; i < lineNumber; i++)
            {
                if (i == lineNumber - 1)
                {
                    // Subtract 1 to account for 1-based column numbering
                    offset += columnNumber - 1;
                }
                else
                {
                    // Add an offset to account for the current platform's newline characters
                    offset += FileLines[i].Length + Environment.NewLine.Length;
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

            ValidatePosition(newLine, newColumn);

            string scriptLine = FileLines[newLine - 1];
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

            BufferPosition startPosition = new(0, 0);
            BufferPosition endPosition = startPosition;

            int line = 0;
            while (line < FileLines.Count)
            {
                if (searchedOffset <= currentOffset + FileLines[line].Length)
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
                currentOffset += FileLines[line].Length + Environment.NewLine.Length;
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
            FileLines = GetLinesInternal(fileContents);

            // Parse the contents to get syntax tree and errors
            ParseFileContents();
        }

        /// <summary>
        /// Parses the current file contents to get the AST, tokens,
        /// and parse errors.
        /// </summary>
        private void ParseFileContents()
        {
            ParseError[] parseErrors = null;

            // First, get the updated file range
            int lineCount = FileLines.Count;
            FileRange = lineCount > 0
                ? new BufferRange(
                        new BufferPosition(1, 1),
                        new BufferPosition(
                            lineCount + 1,
                            FileLines[lineCount - 1].Length + 1))
                : BufferRange.None;

            try
            {
                Token[] scriptTokens;

                // This overload appeared with Windows 10 Update 1
                if (powerShellVersion.Major >= 6 ||
                    (powerShellVersion.Major == 5 && powerShellVersion.Build >= 10586))
                {
                    // Include the file path so that module relative
                    // paths are evaluated correctly
                    ScriptAst =
                        Parser.ParseInput(
                            Contents,
                            FilePath,
                            out scriptTokens,
                            out parseErrors);
                }
                else
                {
                    ScriptAst =
                        Parser.ParseInput(
                            Contents,
                            out scriptTokens,
                            out parseErrors);
                }

                ScriptTokens = scriptTokens;
            }
            catch (RuntimeException ex)
            {
                ParseError parseError =
                    new(
                        null,
                        ex.ErrorRecord.FullyQualifiedErrorId,
                        ex.Message);

                parseErrors = new[] { parseError };
                ScriptTokens = Array.Empty<Token>();
                ScriptAst = null;
            }

            // Translate parse errors into syntax markers
            DiagnosticMarkers =
                parseErrors
                    .Select(ScriptFileMarker.FromParseError)
                    .ToList();

            // Untitled files have no directory
            // Discussed in https://github.com/PowerShell/PowerShellEditorServices/pull/815.
            // Rather than working hard to enable things for untitled files like a phantom directory,
            // users should save the file.
            if (IsInMemory)
            {
                // Need to initialize the ReferencedFiles property to an empty array.
                ReferencedFiles = Array.Empty<string>();
                return;
            }

            // Get all dot sourced referenced files and store them
            ReferencedFiles = AstOperations.FindDotSourcedIncludes(ScriptAst, Path.GetDirectoryName(FilePath));
        }

        #endregion
    }
}
