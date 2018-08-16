//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Commands;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Symbols;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Provides the "reference" code lens by extracting document symbols.
    /// </summary>
    internal class ReferencesCodeLensProvider : FeatureProviderBase, ICodeLensProvider
    {
        private static readonly Location[] s_emptyLocationArray = new Location[0];

        /// <summary>
        /// The editor session code lenses are being provided from.
        /// </summary>
        private EditorSession _editorSession;

        /// <summary>
        /// The document symbol provider to supply symbols to generate the code lenses.
        /// </summary>
        private IDocumentSymbolProvider _symbolProvider;

        /// <summary>
        /// Construct a new ReferencesCodeLensProvider for a given EditorSession.
        /// </summary>
        /// <param name="editorSession"></param>
        public ReferencesCodeLensProvider(EditorSession editorSession)
        {
            _editorSession = editorSession;

            // TODO: Pull this from components
            _symbolProvider = new ScriptDocumentSymbolProvider(
                editorSession.PowerShellContext.LocalPowerShellVersion.Version);
        }

        /// <summary>
        /// Get all reference code lenses for a given script file.
        /// </summary>
        /// <param name="scriptFile">The PowerShell script file to get code lenses for.</param>
        /// <returns>An array of CodeLenses describing all functions in the given script file.</returns>
        public CodeLens[] ProvideCodeLenses(ScriptFile scriptFile)
        {
            var acc = new List<CodeLens>();
            foreach (SymbolReference sym in _symbolProvider.ProvideDocumentSymbols(scriptFile))
            {
                if (sym.SymbolType == SymbolType.Function)
                {
                    acc.Add(new CodeLens(this, scriptFile, sym.ScriptRegion));
                }
            }

            return acc.ToArray();
        }

        /// <summary>
        /// Take a codelens and create a new codelens object with updated references.
        /// </summary>
        /// <param name="codeLens">The old code lens to get updated references for.</param>
        /// <param name="cancellationToken">The cancellation token for this request.</param>
        /// <returns>A new code lens object describing the same data as the old one but with updated references.</returns>
        public async Task<CodeLens> ResolveCodeLensAsync(
            CodeLens codeLens,
            CancellationToken cancellationToken)
        {
            ScriptFile[] references = _editorSession.Workspace.ExpandScriptReferences(
                codeLens.File);

            SymbolReference foundSymbol = _editorSession.LanguageService.FindFunctionDefinitionAtLocation(
                codeLens.File,
                codeLens.ScriptExtent.StartLineNumber,
                codeLens.ScriptExtent.StartColumnNumber);

            FindReferencesResult referencesResult = await _editorSession.LanguageService.FindReferencesOfSymbol(
                foundSymbol,
                references,
                _editorSession.Workspace);

            Location[] referenceLocations;
            if (referencesResult == null)
            {
                referenceLocations = s_emptyLocationArray;
            }
            else
            {
                var acc = new List<Location>();
                foreach (SymbolReference foundReference in referencesResult.FoundReferences)
                {
                    if (!NotReferenceDefinition(foundSymbol, foundReference))
                    {
                        continue;
                    }

                    acc.Add(new Location
                    {
                        Uri = GetFileUri(foundReference.FilePath),
                        Range = foundReference.ScriptRegion.ToRange()
                    });
                }
                referenceLocations = acc.ToArray();
            }

            return new CodeLens(
                codeLens,
                new ClientCommand(
                    "editor.action.showReferences",
                    GetReferenceCountHeader(referenceLocations.Length),
                    new object[]
                    {
                        codeLens.File.ClientFilePath,
                        codeLens.ScriptExtent.ToRange().Start,
                        referenceLocations,
                    }
                    ));
        }

        /// <summary>
        /// Check whether a SymbolReference is not a reference to another defined symbol.
        /// </summary>
        /// <param name="definition">The symbol definition that may be referenced.</param>
        /// <param name="reference">The reference symbol to check.</param>
        /// <returns>True if the reference is not a reference to the definition, false otherwise.</returns>
        private static bool NotReferenceDefinition(
            SymbolReference definition,
            SymbolReference reference)
        {
            return
                definition.ScriptRegion.StartLineNumber != reference.ScriptRegion.StartLineNumber
                || definition.SymbolType != reference.SymbolType
                || !string.Equals(definition.SymbolName, reference.SymbolName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get a URI for a given file path.
        /// </summary>
        /// <param name="filePath">A file path that may be prefixed with URI scheme already.</param>
        /// <returns>A URI to the file.</returns>
        private static string GetFileUri(string filePath)
        {
            // If the file isn't untitled, return a URI-style path
            return
                !filePath.StartsWith("untitled") && !filePath.StartsWith("inmemory")
                    ? new Uri("file://" + filePath).AbsoluteUri
                    : filePath;
        }

        /// <summary>
        /// Get the code lens header for the number of references on a definition,
        /// given the number of references.
        /// </summary>
        /// <param name="referenceCount">The number of references found for a given definition.</param>
        /// <returns>The header string for the reference code lens.</returns>
        private static string GetReferenceCountHeader(int referenceCount)
        {
            if (referenceCount == 1)
            {
                return "1 reference";
            }

            var sb = new StringBuilder(14); // "100 references".Length = 14
            sb.Append(referenceCount);
            sb.Append(" references");
            return sb.ToString();
        }
    }
}
