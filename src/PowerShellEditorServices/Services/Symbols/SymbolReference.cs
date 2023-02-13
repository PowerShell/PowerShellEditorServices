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
    [DebuggerDisplay("Type = {Type}, Id = {Id}, Name = {Name}")]
    internal record SymbolReference
    {
        public SymbolType Type { get; init; }

        public string Id { get; init; }

        public string Name { get; init; }

        public ScriptRegion NameRegion { get; init; }

        public ScriptRegion ScriptRegion { get; init; }

        public string SourceLine { get; internal set; }

        public string FilePath { get; internal set; }

        public bool IsDeclaration { get; init; }

        /// <summary>
        /// Constructs and instance of a SymbolReference
        /// </summary>
        /// <param name="type">The higher level type of the symbol</param>
        /// <param name="id">The name of the symbol</param>
        /// <param name="name">The string used by outline, hover, etc.</param>
        /// <param name="nameExtent">The extent of the symbol's name</param>
        /// <param name="scriptExtent">The script extent of the symbol</param>
        /// <param name="file">The script file that has the symbol</param>
        /// <param name="isDeclaration">True if this reference is the definition of the symbol</param>
        public SymbolReference(
            SymbolType type,
            string id,
            string name,
            IScriptExtent nameExtent,
            IScriptExtent scriptExtent,
            ScriptFile file,
            bool isDeclaration)
        {
            Type = type;
            Id = id;
            Name = name;
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

        /// <summary>
        /// This is only used for unit tests!
        /// </summary>
        internal SymbolReference(string id, SymbolType type)
        {
            Id = id;
            Type = type;
            Name = "";
            NameRegion = new("", "", 0, 0, 0, 0, 0, 0);
            ScriptRegion = NameRegion;
            SourceLine = "";
            FilePath = "";
        }
    }
}
