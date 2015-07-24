//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Contains the details and contents of an open script file.
    /// </summary>
    public class ScriptFile
    {
        #region Private Fields

        private Token[] scriptTokens;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the  path at which this file resides.
        /// </summary>
        public string FilePath { get; private set; }

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
        /// <param name="textReader">The TextReader to use for reading the file's contents.</param>
        public ScriptFile(string filePath, TextReader textReader)
        {
            this.FilePath = filePath;
            this.ReadFile(textReader);
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
            // TODO: Validate range

            return this.FileLines[lineNumber - 1];
        }

        /// <summary>
        /// Applies the provided FileChange to the file's contents
        /// </summary>
        /// <param name="fileChange">The FileChange to apply to the file's contents.</param>
        public void ApplyChange(FileChange fileChange)
        {
            // TODO: Verify offsets are in range

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
        /// Finds the dot sourced files in this ScriptFile
        /// </summary>
        public void FindDotSourcedFiles()
        {
            ReferencedFiles =
                AstOperations.FindDotSourcedIncludes(this.ScriptAst);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Reads the contents of a file contained in the given TextReader.
        /// </summary>
        /// <param name="textReader">A TextReader to use for reading file contents.</param>
        private void ReadFile(TextReader textReader)
        {
            this.FileLines = new List<string>();

            // Read the file contents line by line
            string fileLine = null;
            do
            {
                fileLine = textReader.ReadLine();
                if (fileLine != null)
                {
                    FileLines.Add(fileLine);
                }
            } 
            while (fileLine != null);

            // Parse the contents to get syntax tree and errors
            this.ParseFileContents();
            this.FindDotSourcedFiles();
        }

        /// <summary>
        /// Parses the current file contents to get the AST, tokens,
        /// and parse errors.
        /// </summary>
        private void ParseFileContents()
        {
            ParseError[] parseErrors = null;

            try
            {
                this.ScriptAst =
                    Parser.ParseInput(
                        this.Contents, 
                        out this.scriptTokens, 
                        out parseErrors);
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
        }

        #endregion
    }
}
