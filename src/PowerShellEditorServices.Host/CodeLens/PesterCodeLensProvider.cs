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
        private static char[] QuoteChars = new char[] { '\'', '"'};

        private EditorSession editorSession;
        private IDocumentSymbolProvider symbolProvider;

        public PesterCodeLensProvider(EditorSession editorSession)
        {
            this.editorSession = editorSession;
            this.symbolProvider = new PesterDocumentSymbolProvider();
        }

        private IEnumerable<CodeLens> GetPesterLens(
            SymbolReference symbol,
            ScriptFile scriptFile)
        {
            // Trim the Describe "" { from the symbol name
            int startQuoteIndex = symbol.SourceLine.IndexOfAny(QuoteChars) + 1;
            int endQuoteIndex =  symbol.SourceLine.LastIndexOfAny(QuoteChars);

            string describeBlockName =
                symbol.SourceLine.Substring(
                    startQuoteIndex,
                    endQuoteIndex - startQuoteIndex);

            var clientCommands = new ClientCommand[]
            {
                new ClientCommand(
                    "PowerShell.RunPesterTests",
                    "run tests",
                    new object[]
                    {
                        scriptFile.ClientFilePath,
                        false, // Don't debug
                        describeBlockName,
                    }),

                new ClientCommand(
                    "PowerShell.RunPesterTests",
                    "debug tests",
                    new object[]
                    {
                        scriptFile.ClientFilePath,
                        true, // Run in debugger
                        describeBlockName,
                    }),
            };

            return
                clientCommands.Select(
                    command =>
                        new CodeLens(
                            this,
                            scriptFile,
                            symbol.ScriptRegion,
                            command));
        }

        public CodeLens[] ProvideCodeLenses(ScriptFile scriptFile)
        {
            var symbols =
                this.symbolProvider
                    .ProvideDocumentSymbols(scriptFile);

            var lenses =
                symbols
                    .Where(s => s.SymbolName.StartsWith("Describe"))
                    .SelectMany(s => this.GetPesterLens(s, scriptFile))
                    .ToArray();

            return lenses;
        }

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
