//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Symbols
{
    /// <summary>
    /// Provides an IDocumentSymbolProvider implementation for
    /// enumerating test symbols in Pester test (tests.ps1) files.
    /// </summary>
    public class PesterDocumentSymbolProvider : FeatureProviderBase, IDocumentSymbolProvider
    {
        private static char[] DefinitionTrimChars = new char[] { ' ', '{' };

        IEnumerable<SymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            if (!scriptFile.FilePath.EndsWith(
                    "tests.ps1",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Empty<SymbolReference>();
            }

            var commandAsts = scriptFile.ScriptAst.FindAll(ast =>
            {
                switch ((ast as CommandAst)?.GetCommandName()?.ToLower())
                {
                    case "describe":
                    case "context":
                    case "it":
                        return true;

                    default:
                        return false;
                }
            },
            true);

            return commandAsts.Select(
                ast => {
                    var testDefinitionLine =
                        scriptFile.GetLine(
                            ast.Extent.StartLineNumber);

                    return
                        new SymbolReference(
                            SymbolType.Function,
                            testDefinitionLine.TrimEnd(DefinitionTrimChars),
                            ast.Extent,
                            scriptFile.FilePath,
                            testDefinitionLine);
                });
        }
    }
}
