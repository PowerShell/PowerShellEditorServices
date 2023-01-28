// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Management.Automation.Language;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    /// <summary>
    /// Contains details about a specific region of text in script file.
    /// </summary>
    public sealed class ScriptRegion : IScriptExtent
    {
        internal TextEdit ToTextEdit() => new() { NewText = Text, Range = ToRange() };

        public Range ToRange()
        {
            return new Range
            {
                Start = new Position
                {
                    Line = StartLineNumber - 1,
                    Character = StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = EndLineNumber - 1,
                    Character = EndColumnNumber - 1
                }
            };
        }

        // Same as PowerShell's EmptyScriptExtent
        internal bool IsEmpty()
        {
            return StartLineNumber == 0 && StartColumnNumber == 0
                && EndLineNumber == 0 && EndColumnNumber == 0
                && string.IsNullOrEmpty(File)
                && string.IsNullOrEmpty(Text);
        }

        // Do not use PowerShell's ContainsLineAndColumn, it's nonsense.
        internal bool ContainsPosition(int line, int column)
        {
            return StartLineNumber <= line && line <= EndLineNumber
                && StartColumnNumber <= column && column <= EndColumnNumber;
        }

        public override string ToString() => $"Start {StartLineNumber}:{StartColumnNumber}, End {EndLineNumber}:{EndColumnNumber}";

        #region Constructors

        public ScriptRegion(
            string file,
            string text,
            int startLineNumber,
            int startColumnNumber,
            int startOffset,
            int endLineNumber,
            int endColumnNumber,
            int endOffset)
        {
            File = file;
            Text = text;
            StartLineNumber = startLineNumber;
            StartColumnNumber = startColumnNumber;
            StartOffset = startOffset;
            EndLineNumber = endLineNumber;
            EndColumnNumber = endColumnNumber;
            EndOffset = endOffset;
        }

        public ScriptRegion (IScriptExtent scriptExtent)
        {
            File = scriptExtent.File;

            // IScriptExtent throws an ArgumentOutOfRange exception if Text is null
            try
            {
                Text = scriptExtent.Text;
            }
            catch (ArgumentOutOfRangeException)
            {
                Text = string.Empty;
            }

            StartLineNumber = scriptExtent.StartLineNumber;
            StartColumnNumber = scriptExtent.StartColumnNumber;
            StartOffset = scriptExtent.StartOffset;
            EndLineNumber = scriptExtent.EndLineNumber;
            EndColumnNumber = scriptExtent.EndColumnNumber;
            EndOffset = scriptExtent.EndOffset;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the file path of the script file in which this region is contained.
        /// </summary>
        public string File { get; }

        /// <summary>
        /// Gets or sets the text that is contained within the region.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets or sets the starting line number of the region.
        /// </summary>
        public int StartLineNumber { get; }

        /// <summary>
        /// Gets or sets the starting column number of the region.
        /// </summary>
        public int StartColumnNumber { get; }

        /// <summary>
        /// Gets or sets the starting file offset of the region.
        /// </summary>
        public int StartOffset { get; }

        /// <summary>
        /// Gets or sets the ending line number of the region.
        /// </summary>
        public int EndLineNumber { get; }

        /// <summary>
        /// Gets or sets the ending column number of the region.
        /// </summary>
        public int EndColumnNumber { get; }

        /// <summary>
        /// Gets or sets the ending file offset of the region.
        /// </summary>
        public int EndOffset { get; }

        /// <summary>
        /// Gets the starting IScriptPosition in the script.
        /// (Currently unimplemented.)
        /// </summary>
        IScriptPosition IScriptExtent.StartScriptPosition => throw new NotImplementedException();

        /// <summary>
        /// Gets the ending IScriptPosition in the script.
        /// (Currently unimplemented.)
        /// </summary>
        IScriptPosition IScriptExtent.EndScriptPosition => throw new NotImplementedException();

        #endregion
    }
}
