using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    internal interface IDocumentSymbolProvider
    {
        IEnumerable<SymbolReference> GetSymbols(ScriptFile scriptFile, Version psVersion);
    }
}