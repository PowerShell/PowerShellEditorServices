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

        // TODO: Have a symbol name and a separate display name, the first minimally the text so the
        // buckets work, the second usually a more complete signature for e.g. outline view.
        public string SymbolName { get; }

        public ScriptRegion NameRegion { get; }

        public ScriptRegion ScriptRegion { get; }

        public string SourceLine { get; internal set; }

        public string FilePath { get; internal set; }

        public bool IsDeclaration { get; }

        /// <summary>
        /// Constructs and instance of a SymbolReference
        /// </summary>
        /// <param name="symbolType">The higher level type of the symbol</param>
        /// <param name="symbolName">The name of the symbol</param>
        /// <param name="nameExtent">The extent of the symbol's name</param>
        /// <param name="scriptExtent">The script extent of the symbol</param>
        /// <param name="file">The script file that has the symbol</param>
        /// <param name="isDeclaration">True if this reference is the definition of the symbol</param>
        public SymbolReference(
            SymbolType symbolType,
            string symbolName,
            IScriptExtent nameExtent,
            IScriptExtent scriptExtent,
            ScriptFile file,
            bool isDeclaration)
        {
            SymbolType = symbolType;
            SymbolName = symbolName;
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
