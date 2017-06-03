using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    internal class GenericDocumentSymbolProvider : IDocumentSymbolProvider
    {
        bool IDocumentSymbolProvider.CanProvideFor(ScriptFile scriptFile)
        {
            return scriptFile != null &&
                scriptFile.FilePath != null &&
                (scriptFile.FilePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                    scriptFile.FilePath.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase));
        }

        IEnumerable<SymbolReference> IDocumentSymbolProvider.GetSymbols(ScriptFile scriptFile, Version psVersion)
        {
            return AstOperations.FindSymbolsInDocument(
                scriptFile.ScriptAst,
                psVersion);
        }
    }
}