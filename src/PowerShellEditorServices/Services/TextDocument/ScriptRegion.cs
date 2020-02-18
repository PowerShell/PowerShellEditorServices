//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
        #region Static Methods

        /// <summary>
        /// Creates a new instance of the ScriptRegion class from an
        /// instance of an IScriptExtent implementation.
        /// </summary>
        /// <param name="scriptExtent">
        /// The IScriptExtent to copy into the ScriptRegion.
        /// </param>
        /// <returns>
        /// A new ScriptRegion instance with the same details as the IScriptExtent.
        /// </returns>
        public static ScriptRegion Create(IScriptExtent scriptExtent)
        {
            // IScriptExtent throws an ArgumentOutOfRange exception if Text is null
            string scriptExtentText;
            try
            {
                scriptExtentText = scriptExtent.Text;
            }
            catch (ArgumentOutOfRangeException)
            {
                scriptExtentText = string.Empty;
            }

            return new ScriptRegion(
                scriptExtent.File,
                scriptExtentText,
                scriptExtent.StartLineNumber,
                scriptExtent.StartColumnNumber,
                scriptExtent.StartOffset,
                scriptExtent.EndLineNumber,
                scriptExtent.EndColumnNumber,
                scriptExtent.EndOffset);
        }

        internal static TextEdit ToTextEdit(ScriptRegion scriptRegion)
        {
            return new TextEdit
            {
                NewText = scriptRegion.Text,
                Range = new Range
                {
                    Start = new Position
                    {
                        Line = scriptRegion.StartLineNumber - 1,
                        Character = scriptRegion.StartColumnNumber - 1,
                    },
                    End = new Position
                    {
                        Line = scriptRegion.EndLineNumber - 1,
                        Character = scriptRegion.EndColumnNumber - 1,
                    }
                }
            };
        }

        #endregion

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
