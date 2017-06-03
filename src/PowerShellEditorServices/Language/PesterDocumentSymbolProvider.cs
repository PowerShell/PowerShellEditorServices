using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    internal class PesterDocumentSymbolProvider : IDocumentSymbolProvider
    {
        IEnumerable<SymbolReference> IDocumentSymbolProvider.GetSymbols(ScriptFile scriptFile, Version psVersion)
        {
            if (IsPesterFile(scriptFile))
            {
                return GetPesterSymbols(scriptFile);
            }

            return Enumerable.Empty<SymbolReference>();
        }

        private IEnumerable<SymbolReference> GetPesterSymbols(ScriptFile scriptFile)
        {
            var commandAsts = scriptFile.ScriptAst.FindAll(ast =>
            {
                switch ((ast as CommandAst)?.GetCommandName().ToLower())
                {
                    case "describe":
                    case "context":
                    case "it":
                        return true;

                    case null:
                    default:
                        return false;
                }
            },
            true);

            return commandAsts.Select(ast => new SymbolReference(
                SymbolType.Function,
                ast.Extent,
                scriptFile.FilePath,
                scriptFile.GetLine(ast.Extent.StartLineNumber)));
        }

        private bool IsPesterFile(ScriptFile scriptFile)
        {
            return scriptFile.FilePath.EndsWith("tests.ps1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
