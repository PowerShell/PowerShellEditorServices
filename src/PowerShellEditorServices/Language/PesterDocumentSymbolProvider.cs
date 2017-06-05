using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    internal class PesterDocumentSymbolProvider : DocumentSymbolProvider
    {
        protected override bool CanProvideFor(ScriptFile scriptFile)
        {
            return scriptFile.FilePath.EndsWith("tests.ps1", StringComparison.OrdinalIgnoreCase);
        }

        protected override IEnumerable<SymbolReference> GetSymbolsImpl(ScriptFile scriptFile, Version psVersion)
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
    }
}
