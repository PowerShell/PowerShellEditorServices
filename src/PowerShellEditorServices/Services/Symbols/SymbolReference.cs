// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System.Diagnostics;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// A class that holds the type, name, script extent, and source line of a symbol
    /// </summary>
    [DebuggerDisplay("SymbolType = {SymbolType}, SymbolName = {SymbolName}")]
    internal record SymbolReference
    {
        public SymbolType SymbolType { get; }

        public string SymbolName { get; }

        public ScriptRegion ScriptRegion { get; }

        public string SourceLine { get; internal set; }

        public string FilePath { get; internal set; }

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
            SymbolType = symbolType;
            SymbolName = symbolName;
            ScriptRegion = new(scriptExtent);
            FilePath = filePath;
            SourceLine = sourceLine;
        }

        public SymbolReference(
            SymbolType symbolType,
            string symbolName,
            IScriptExtent scriptExtent,
            ScriptFile file)
        {
            SymbolType = symbolType;
            SymbolName = symbolName;
            ScriptRegion = new(scriptExtent);
            FilePath = file.FilePath;
            try
            {
                SourceLine = file.GetLine(ScriptRegion.StartLineNumber);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                SourceLine = string.Empty;
            }
        }

        /// <summary>
        /// Constructs an instance of a SymbolReference
        /// </summary>
        /// <param name="symbolType">The higher level type of the symbol</param>
        /// <param name="scriptExtent">The script extent of the symbol</param>
        public SymbolReference(SymbolType symbolType, IScriptExtent scriptExtent)
            : this(symbolType, scriptExtent.Text, scriptExtent, scriptExtent.File, "")
        {
        }
    }
}
