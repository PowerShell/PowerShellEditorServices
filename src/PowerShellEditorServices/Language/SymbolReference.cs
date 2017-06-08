﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// A class that holds the type, name, script extent, and source line of a symbol
    /// </summary>
    [DebuggerDisplay("SymbolType = {SymbolType}, SymbolName = {SymbolName}")]
    public class SymbolReference
    {
        #region Properties

        /// <summary>
        /// Gets the symbol's type
        /// </summary>
        public SymbolType SymbolType { get; private set; }

        /// <summary>
        /// Gets the name of the symbol
        /// </summary>
        public string SymbolName { get; private set; }

        /// <summary>
        /// Gets the script extent of the symbol
        /// </summary>
        public ScriptRegion ScriptRegion { get; private set; }

        /// <summary>
        /// Gets the contents of the line the given symbol is on
        /// </summary>
        public string SourceLine { get; internal set; }

        /// <summary>
        /// Gets the path of the file in which the symbol was found.
        /// </summary>
        public string FilePath { get; internal set; }

        #endregion

        /// <summary>
        /// Constructs and instance of a SymbolReference
        /// </summary>
        /// <param name="symbolType">The higher level type of the symbol</param>
        /// <param name="symbolName">The name of the symbol</param>
        /// <param name="scriptExtent">The script extent of the symbol</param>
        /// <param name="filePath">The file path of the symbol</param>
        /// <param name="sourceLine">The line contents of the given symbol (defaults to empty string)</param>
        public SymbolReference(
            SymbolType symbolType,
            string symbolName,
            IScriptExtent scriptExtent,
            string filePath = "",
            string sourceLine = "")
        {
            // TODO: Verify params
            this.SymbolType = symbolType;
            this.SymbolName = symbolName;
            this.ScriptRegion = ScriptRegion.Create(scriptExtent);
            this.FilePath = filePath;
            this.SourceLine = sourceLine;

            // TODO: Make sure end column number usage is correct

            // Build the display string
            //this.DisplayString =
            //    string.Format(
            //        "{0} {1}")
        }

        /// <summary>
        /// Constructs and instance of a SymbolReference
        /// </summary>
        /// <param name="symbolType">The higher level type of the symbol</param>
        /// <param name="scriptExtent">The script extent of the symbol</param>
        /// <param name="filePath">The file path of the symbol</param>
        /// <param name="sourceLine">The line contents of the given symbol (defaults to empty string)</param>
        public SymbolReference(SymbolType symbolType, IScriptExtent scriptExtent, string filePath = "", string sourceLine = "")
            : this(symbolType, scriptExtent.Text, scriptExtent, filePath, sourceLine)
        {
        }
    }
}
