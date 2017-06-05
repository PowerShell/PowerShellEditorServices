using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    internal class ScriptDocumentSymbolProvider : DocumentSymbolProvider
    {
        protected override bool CanProvideFor(ScriptFile scriptFile)
        {
            return scriptFile != null &&
                scriptFile.FilePath != null &&
                (scriptFile.FilePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                    scriptFile.FilePath.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase));
        }

        protected override IEnumerable<SymbolReference> GetSymbolsImpl(
            ScriptFile scriptFile,
            Version psVersion)
        {
            return AstOperations.FindSymbolsInDocument(
                                scriptFile.ScriptAst,
                                psVersion);

        }
    }
}
