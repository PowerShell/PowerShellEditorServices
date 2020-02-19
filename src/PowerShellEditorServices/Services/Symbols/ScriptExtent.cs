//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Provides a default IScriptExtent implementation
    /// containing details about a section of script content
    /// in a file.
    /// </summary>
    internal class ScriptExtent : IScriptExtent
    {
        #region Properties

        /// <summary>
        /// Gets the file path of the script file in which this extent is contained.
        /// </summary>
        public string File
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the starting column number of the extent.
        /// </summary>
        public int StartColumnNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the starting line number of the extent.
        /// </summary>
        public int StartLineNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the starting file offset of the extent.
        /// </summary>
        public int StartOffset
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the starting script position of the extent.
        /// </summary>
        public IScriptPosition StartScriptPosition
        {
            get { throw new NotImplementedException(); }
        }
        /// <summary>
        /// Gets or sets the text that is contained within the extent.
        /// </summary>
        public string Text
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the ending column number of the extent.
        /// </summary>
        public int EndColumnNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the ending line number of the extent.
        /// </summary>
        public int EndLineNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the ending file offset of the extent.
        /// </summary>
        public int EndOffset
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the ending script position of the extent.
        /// </summary>
        public IScriptPosition EndScriptPosition
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
}
