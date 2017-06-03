using System;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    internal class PSDDocumentSymbolProvider : IDocumentSymbolProvider
    {
        IEnumerable<SymbolReference> IDocumentSymbolProvider.GetSymbols(ScriptFile scriptFile, Version psVersion)
        {
            if (!CanProvideFor(scriptFile))
            {
                return Enumerable.Empty<SymbolReference>();
            }

            var findHashtableSymbolsVisitor = new FindHashtableSymbolsVisitor();
            scriptFile.ScriptAst.Visit(findHashtableSymbolsVisitor);
            return findHashtableSymbolsVisitor.SymbolReferences;
        }

        private bool CanProvideFor(ScriptFile scriptFile)
        {
            return (scriptFile.FilePath != null &&
                    scriptFile.FilePath.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase)) ||
                    AstOperations.IsPowerShellDataFileAst(scriptFile.ScriptAst);
        }
    }
}
