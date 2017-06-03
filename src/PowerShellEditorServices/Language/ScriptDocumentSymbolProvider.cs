using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices
{
    internal class ScriptDocumentSymbolProvider : IDocumentSymbolProvider
    {
        IEnumerable<SymbolReference> IDocumentSymbolProvider.GetSymbols(ScriptFile scriptFile, Version psVersion)
        {
            if (CanProvideFor(scriptFile))
            {
                return AstOperations.FindSymbolsInDocument(
                    scriptFile.ScriptAst,
                    psVersion);
            }

            return Enumerable.Empty<SymbolReference>();
        }

        private bool CanProvideFor(ScriptFile scriptFile)
        {
            return scriptFile != null &&
                scriptFile.FilePath != null &&
                (scriptFile.FilePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                    scriptFile.FilePath.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase));
        }
    }
}