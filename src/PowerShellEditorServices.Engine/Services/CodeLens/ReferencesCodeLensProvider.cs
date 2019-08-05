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
using Microsoft.PowerShell.EditorServices.Symbols;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using PowerShellEditorServices.Engine.Utility;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Provides the "reference" code lens by extracting document symbols.
    /// </summary>
    internal class ReferencesCodeLensProvider : ICodeLensProvider
    {
        private static readonly Location[] s_emptyLocationArray = new Location[0];

        /// <summary>
        /// The document symbol provider to supply symbols to generate the code lenses.
        /// </summary>
        private IDocumentSymbolProvider _symbolProvider;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        /// <summary>
        /// Specifies a unique identifier for the feature provider, typically a
        /// fully-qualified name like "Microsoft.PowerShell.EditorServices.MyProvider"
        /// </summary>
        public string ProviderId => nameof(ReferencesCodeLensProvider);

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
            _symbolProvider = new ScriptDocumentSymbolProvider(
                VersionUtils.PSVersion);
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
                    //acc.Add(new CodeLens(this, scriptFile, sym.ScriptRegion));
                    acc.Add(new CodeLens()
                    {
                        Data = JToken.FromObject(new
                        {
                            Uri = scriptFile.DocumentUri,
                            ProviderId = nameof(ReferencesCodeLensProvider)
                        }),
                        Range = sym.ScriptRegion.ToRange()
                    });
                }
            }

            return acc.ToArray();
        }

        /// <summary>
        /// Take a codelens and create a new codelens object with updated references.
        /// </summary>
        /// <param name="codeLens">The old code lens to get updated references for.</param>
        /// <returns>A new code lens object describing the same data as the old one but with updated references.</returns>
        public CodeLens ResolveCodeLens(CodeLens codeLens, ScriptFile scriptFile)
        {

            ScriptFile[] references = _workspaceService.ExpandScriptReferences(
                scriptFile);

            SymbolReference foundSymbol = _symbolsService.FindFunctionDefinitionAtLocation(
                scriptFile,
                (int)codeLens.Range.Start.Line + 1,
                (int)codeLens.Range.Start.Character + 1);

            List<SymbolReference> referencesResult = _symbolsService.FindReferencesOfSymbol(
                foundSymbol,
                references,
                _workspaceService);

            Location[] referenceLocations;
            if (referencesResult == null)
            {
                referenceLocations = s_emptyLocationArray;
            }
            else
            {
                var acc = new List<Location>();
                foreach (SymbolReference foundReference in referencesResult)
                {
                    if (!NotReferenceDefinition(foundSymbol, foundReference))
                    {
                        continue;
                    }

                    acc.Add(new Location
                    {
                        Uri = PathUtils.ToUri(foundReference.FilePath),
                        Range = foundReference.ScriptRegion.ToRange()
                    });
                }
                referenceLocations = acc.ToArray();
            }

            return new CodeLens()
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
                    })
                }
            };
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
