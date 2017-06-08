//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Commands;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Symbols;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    internal class ReferencesCodeLensProvider : FeatureProviderBase, ICodeLensProvider
    {
        private EditorSession editorSession;
        private IDocumentSymbolProvider symbolProvider;

        public ReferencesCodeLensProvider(EditorSession editorSession)
        {
            this.editorSession = editorSession;

            // TODO: Pull this from components
            this.symbolProvider =
                new ScriptDocumentSymbolProvider(
                    editorSession.PowerShellContext.LocalPowerShellVersion.Version);
        }

        public CodeLens[] ProvideCodeLenses(ScriptFile scriptFile)
        {
            return
                this.symbolProvider
                    .ProvideDocumentSymbols(scriptFile)
                    .Where(symbol => symbol.SymbolType == SymbolType.Function)
                    .Select(
                        symbol =>
                            new CodeLens(
                                this,
                                scriptFile,
                                symbol.ScriptRegion))
                    .ToArray();
        }

        public async Task<CodeLens> ResolveCodeLensAsync(
            CodeLens codeLens,
            CancellationToken cancellationToken)
        {
            ScriptFile[] references =
                editorSession.Workspace.ExpandScriptReferences(
                    codeLens.File);

            var foundSymbol =
                this.editorSession.LanguageService.FindFunctionDefinitionAtLocation(
                    codeLens.File,
                    codeLens.ScriptExtent.StartLineNumber,
                    codeLens.ScriptExtent.StartColumnNumber);

            FindReferencesResult referencesResult =
                await editorSession.LanguageService.FindReferencesOfSymbol(
                    foundSymbol,
                    references,
                    editorSession.Workspace);

            var referenceLocations =
                referencesResult
                    .FoundReferences
                    .Select(
                        r => new Location
                        {
                            Uri = GetFileUri(r.FilePath),
                            Range = r.ScriptRegion.ToRange()
                        })
                    .ToArray();

            return
                new CodeLens(
                    codeLens,
                    new ClientCommand(
                        "editor.action.showReferences",
                        referenceLocations.Length == 1
                            ? "1 reference"
                            : $"{referenceLocations.Length} references",
                        new object[]
                        {
                            codeLens.File.ClientFilePath,
                            codeLens.ScriptExtent.ToRange().Start,
                            referenceLocations,
                        }
                    ));
        }

        private static string GetFileUri(string filePath)
        {
            // If the file isn't untitled, return a URI-style path
            return
                !filePath.StartsWith("untitled")
                    ? new Uri("file://" + filePath).AbsoluteUri
                    : filePath;
        }
    }
}