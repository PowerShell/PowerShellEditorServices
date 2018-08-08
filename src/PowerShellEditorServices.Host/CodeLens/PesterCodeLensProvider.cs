//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Commands;
using Microsoft.PowerShell.EditorServices.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    internal class PesterCodeLensProvider : FeatureProviderBase, ICodeLensProvider
    {
        /// <summary>
        /// The editor session context to provide CodeLenses for.
        /// </summary>
        private EditorSession _editorSession;

        /// <summary>
        /// The symbol provider to get symbols from to build code lenses with.
        /// </summary>
        private IDocumentSymbolProvider _symbolProvider;

        /// <summary>
        /// Create a new Pester CodeLens provider for a given editor session.
        /// </summary>
        /// <param name="editorSession">The editor session context for which to provide Pester CodeLenses.</param>
        public PesterCodeLensProvider(EditorSession editorSession)
        {
            _editorSession = editorSession;
            _symbolProvider = new PesterDocumentSymbolProvider();
        }

        /// <summary>
        /// Get the Pester CodeLenses for a given Pester symbol.
        /// </summary>
        /// <param name="pesterSymbol">The Pester symbol to get CodeLenses for.</param>
        /// <param name="scriptFile">The script file the Pester symbol comes from.</param>
        /// <returns>All CodeLenses for the given Pester symbol.</returns>
        private CodeLens[] GetPesterLens(
            PesterSymbolReference pesterSymbol,
            ScriptFile scriptFile)
        {
            var codeLensResults = new CodeLens[]
            {
                new CodeLens(
                    this,
                    scriptFile,
                    pesterSymbol.ScriptRegion,
                    new ClientCommand(
                        "PowerShell.RunPesterTests",
                        "Run tests",
                        new object[] { scriptFile.ClientFilePath, false /* No debug */, pesterSymbol.TestName })),

                new CodeLens(
                    this,
                    scriptFile,
                    pesterSymbol.ScriptRegion,
                    new ClientCommand(
                        "PowerShell.RunPesterTests",
                        "Debug tests",
                        new object[] { scriptFile.ClientFilePath, true /* Run in debugger */, pesterSymbol.TestName })),
            };

            return codeLensResults;
        }

        /// <summary>
        /// Get all Pester CodeLenses for a given script file.
        /// </summary>
        /// <param name="scriptFile">The script file to get Pester CodeLenses for.</param>
        /// <returns>All Pester CodeLenses for the given script file.</returns>
        public CodeLens[] ProvideCodeLenses(ScriptFile scriptFile)
        {
            var lenses = new List<CodeLens>();
            foreach (SymbolReference symbol in _symbolProvider.ProvideDocumentSymbols(scriptFile))
            {
                if (symbol is PesterSymbolReference pesterSymbol)
                {
                    if (pesterSymbol.Command != PesterCommandType.Describe)
                    {
                        continue;
                    }

                    lenses.AddRange(GetPesterLens(pesterSymbol, scriptFile));
                }
            }

            return lenses.ToArray();
        }

        /// <summary>
        /// Resolve the CodeLens provision asynchronously -- just wraps the CodeLens argument in a task.
        /// </summary>
        /// <param name="codeLens">The code lens to resolve.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The given CodeLens, wrapped in a task.</returns>
        public Task<CodeLens> ResolveCodeLensAsync(
            CodeLens codeLens,
            CancellationToken cancellationToken)
        {
            // This provider has no specific behavior for
            // resolving CodeLenses.
            return Task.FromResult(codeLens);
        }
    }
}
