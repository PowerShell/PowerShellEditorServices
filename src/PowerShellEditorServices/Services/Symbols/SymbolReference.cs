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
        public SymbolType SymbolType { get; init; }

        public string SymbolName { get; init; }

        public string DisplayString { get; init; }

        public ScriptRegion NameRegion { get; init; }

        public ScriptRegion ScriptRegion { get; init; }

        public string SourceLine { get; internal set; }

        public string FilePath { get; internal set; }

        public bool IsDeclaration { get; init; }

        /// <summary>
        /// Constructs and instance of a SymbolReference
        /// </summary>
        /// <param name="symbolType">The higher level type of the symbol</param>
        /// <param name="symbolName">The name of the symbol</param>
        /// <param name="displayString">The string used by outline, hover, etc.</param>
        /// <param name="nameExtent">The extent of the symbol's name</param>
        /// <param name="scriptExtent">The script extent of the symbol</param>
        /// <param name="file">The script file that has the symbol</param>
        /// <param name="isDeclaration">True if this reference is the definition of the symbol</param>
        public SymbolReference(
            SymbolType symbolType,
            string symbolName,
            string displayString,
            IScriptExtent nameExtent,
            IScriptExtent scriptExtent,
            ScriptFile file,
            bool isDeclaration)
        {
            SymbolType = symbolType;
            SymbolName = symbolName;
            DisplayString = displayString;
            NameRegion = new(nameExtent);
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
            IsDeclaration = isDeclaration;
        }
    }
}
