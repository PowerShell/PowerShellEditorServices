//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Contains details about a specific region of text in script file.
    /// </summary>
    public sealed class ScriptRegion
    {
        #region Properties

        /// <summary>
        /// Gets the file path of the script file in which this region is contained.
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// Gets or sets the text that is contained within the region.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the starting line number of the region.
        /// </summary>
        public int StartLineNumber { get; set; }

        /// <summary>
        /// Gets or sets the starting column number of the region.
        /// </summary>
        public int StartColumnNumber { get; set; }

        /// <summary>
        /// Gets or sets the starting file offset of the region.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// Gets or sets the ending line number of the region.
        /// </summary>
        public int EndLineNumber { get; set; }

        /// <summary>
        /// Gets or sets the ending column number of the region.
        /// </summary>
        public int EndColumnNumber { get; set; }

        /// <summary>
        /// Gets or sets the ending file offset of the region.
        /// </summary>
        public int EndOffset { get; set; }

        #endregion

        #region Constructors

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
            return new ScriptRegion
            {
                File = scriptExtent.File,
                Text = scriptExtent.Text,
                StartLineNumber = scriptExtent.StartLineNumber,
                StartColumnNumber = scriptExtent.StartColumnNumber,
                StartOffset = scriptExtent.StartOffset,
                EndLineNumber = scriptExtent.EndLineNumber,
                EndColumnNumber = scriptExtent.EndColumnNumber,
                EndOffset = scriptExtent.EndOffset
            };
        }

        #endregion
    }
}
