using System;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    internal class PSDataFileDocumentSymbolProvider : IDocumentSymbolProvider
    {
        bool IDocumentSymbolProvider.CanProvideFor(ScriptFile scriptFile)
        {
            throw new NotImplementedException();
        }

        IEnumerable<SymbolReference> IDocumentSymbolProvider.GetSymbols(ScriptFile scriptFile, Version psVersion)
        {
            if (!AstOperations.IsPowerShellDataFileAst(scriptFile.ScriptAst))
            {
                return Enumerable.Empty<SymbolReference>();
            }

            var findHashtableSymbolsVisitor = new FindHashtableSymbolsVisitor();
            scriptFile.ScriptAst.Visit(findHashtableSymbolsVisitor);
            return findHashtableSymbolsVisitor.SymbolReferences;
        }
    }
}