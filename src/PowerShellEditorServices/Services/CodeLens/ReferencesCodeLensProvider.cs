// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Provides the "reference" code lens by extracting document symbols.
    /// </summary>
    internal class ReferencesCodeLensProvider : ICodeLensProvider
    {
        /// <summary>
        /// The document symbol provider to supply symbols to generate the code lenses.
        /// </summary>
        private readonly IDocumentSymbolProvider _symbolProvider;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        public static string Id => nameof(ReferencesCodeLensProvider);

        /// <summary>
        /// Specifies a unique identifier for the feature provider, typically a
        /// fully-qualified name like "Microsoft.PowerShell.EditorServices.MyProvider"
        /// </summary>
        public string ProviderId => Id;

        /// <summary>
        /// Construct a new ReferencesCodeLensProvider for a given EditorSession.
        /// </summary>
        /// <param name="workspaceService"></param>
        /// <param name="symbolsService"></param>
        public ReferencesCodeLensProvider(WorkspaceService workspaceService, SymbolsService symbolsService)
        {
            _workspaceService = workspaceService;
            _symbolsService = symbolsService;
            // TODO: Pull this from components
            _symbolProvider = new ScriptDocumentSymbolProvider();
        }

        /// <summary>
        /// Get all reference code lenses for a given script file.
        /// </summary>
        /// <param name="scriptFile">The PowerShell script file to get code lenses for.</param>
        /// <returns>An IEnumerable of CodeLenses describing all functions, classes and enums in the given script file.</returns>
        public IEnumerable<CodeLens> ProvideCodeLenses(ScriptFile scriptFile)
        {
            foreach (SymbolReference symbol in _symbolProvider.ProvideDocumentSymbols(scriptFile))
            {
                // TODO: Can we support more here?
                if (symbol.IsDeclaration &&
                    symbol.Type is
                    SymbolType.Function or
                    SymbolType.Class or
                    SymbolType.Enum)
                {
                    yield return new CodeLens
                    {
                        Data = JToken.FromObject(new
                        {
                            Uri = scriptFile.DocumentUri,
                            ProviderId = nameof(ReferencesCodeLensProvider)
                        }, LspSerializer.Instance.JsonSerializer),
                        Range = symbol.NameRegion.ToRange(),
                    };
                }
            }
        }

        /// <summary>
        /// Take a CodeLens and create a new CodeLens object with updated references.
        /// </summary>
        /// <param name="codeLens">The old code lens to get updated references for.</param>
        /// <param name="scriptFile"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A new CodeLens object describing the same data as the old one but with updated references.</returns>
        public async Task<CodeLens> ResolveCodeLens(
            CodeLens codeLens,
            ScriptFile scriptFile,
            CancellationToken cancellationToken)
        {
            SymbolReference foundSymbol = SymbolsService.FindSymbolDefinitionAtLocation(
                scriptFile,
                codeLens.Range.Start.Line + 1,
                codeLens.Range.Start.Character + 1);

            List<Location> acc = new();
            foreach (SymbolReference foundReference in await _symbolsService.ScanForReferencesOfSymbolAsync(
                foundSymbol, cancellationToken).ConfigureAwait(false))
            {
                // We only show lenses on declarations, so we exclude those from the references.
                if (foundReference.IsDeclaration)
                {
                    continue;
                }

                DocumentUri uri = DocumentUri.From(foundReference.FilePath);
                // For any vscode-notebook-cell, we need to ignore the backing file on disk.
                if (uri.Scheme == "file" &&
                    scriptFile.DocumentUri.Scheme == "vscode-notebook-cell" &&
                    uri.Path == scriptFile.DocumentUri.Path)
                {
                    continue;
                }

                acc.Add(new Location
                {
                    Uri = uri,
                    Range = foundReference.NameRegion.ToRange()
                });
            }

            Location[] referenceLocations = acc.ToArray();
            return new CodeLens
            {
                Data = codeLens.Data,
                Range = codeLens.Range,
                Command = new Command
                {
                    Name = "editor.action.showReferences",
                    Title = GetReferenceCountHeader(referenceLocations.Length),
                    Arguments = JArray.FromObject(new object[]
                    {
                        scriptFile.DocumentUri,
                        codeLens.Range.Start,
                        referenceLocations
                    },
                    LspSerializer.Instance.JsonSerializer)
                }
            };
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

            StringBuilder sb = new(14); // "100 references".Length = 14
            sb.Append(referenceCount);
            sb.Append(" references");
            return sb.ToString();
        }
    }
}
