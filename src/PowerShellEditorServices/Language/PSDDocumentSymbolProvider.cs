using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    internal class PSDDocumentSymbolProvider : DocumentSymbolProvider
    {
        protected override bool CanProvideFor(ScriptFile scriptFile)
        {
            return (scriptFile.FilePath != null &&
                    scriptFile.FilePath.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase)) ||
                    AstOperations.IsPowerShellDataFileAst(scriptFile.ScriptAst);
        }
        protected override IEnumerable<SymbolReference> GetSymbolsImpl(ScriptFile scriptFile, Version psVersion)
        {
            var findHashtableSymbolsVisitor = new FindHashtableSymbolsVisitor();
            scriptFile.ScriptAst.Visit(findHashtableSymbolsVisitor);
            return findHashtableSymbolsVisitor.SymbolReferences;
        }
    }
}
