//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    internal class PesterCodeLensProvider : ICodeLensProvider
    {
        private readonly ConfigurationService _configurationService;

        /// <summary>
        /// The symbol provider to get symbols from to build code lenses with.
        /// </summary>
        private readonly IDocumentSymbolProvider _symbolProvider;

        /// <summary>
        /// Specifies a unique identifier for the feature provider, typically a
        /// fully-qualified name like "Microsoft.PowerShell.EditorServices.MyProvider"
        /// </summary>
        public string ProviderId => nameof(PesterCodeLensProvider);

        /// <summary>
        /// Create a new Pester CodeLens provider for a given editor session.
        /// </summary>
        public PesterCodeLensProvider(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            _symbolProvider = new PesterDocumentSymbolProvider();
        }

        /// <summary>
        /// Get the Pester CodeLenses for a given Pester symbol.
        /// </summary>
        /// <param name="pesterSymbol">The Pester symbol to get CodeLenses for.</param>
        /// <param name="scriptFile">The script file the Pester symbol comes from.</param>
        /// <returns>All CodeLenses for the given Pester symbol.</returns>
        private CodeLens[] GetPesterLens(PesterSymbolReference pesterSymbol, ScriptFile scriptFile)
        {
            string word = pesterSymbol.Command == PesterCommandType.It ? "test" : "tests";
            var codeLensResults = new CodeLens[]
            {
                new CodeLens()
                {
                    Range = pesterSymbol.ScriptRegion.ToRange(),
                    Data = JToken.FromObject(new {
                        Uri = scriptFile.DocumentUri,
                        ProviderId = nameof(PesterCodeLensProvider)
                    }, Serializer.Instance.JsonSerializer),
                    Command = new Command()
                    {
                        Name = "PowerShell.RunPesterTests",
                        Title = $"Run {word}",
                        Arguments = JArray.FromObject(new object[]
                        {
                            scriptFile.DocumentUri,
                            false /* No debug */,
                            pesterSymbol.TestName,
                            pesterSymbol.ScriptRegion?.StartLineNumber
                        }, Serializer.Instance.JsonSerializer)
                    }
                },

                new CodeLens()
                {
                    Range = pesterSymbol.ScriptRegion.ToRange(),
                    Data = JToken.FromObject(new {
                        Uri = scriptFile.DocumentUri,
                        ProviderId = nameof(PesterCodeLensProvider)
                    }, Serializer.Instance.JsonSerializer),
                    Command = new Command()
                    {
                        Name = "PowerShell.RunPesterTests",
                        Title = $"Debug {word}",
                        Arguments = JArray.FromObject(new object[]
                        {
                            scriptFile.DocumentUri,
                            true /* No debug */,
                            pesterSymbol.TestName,
                            pesterSymbol.ScriptRegion?.StartLineNumber
                        },
                        Serializer.Instance.JsonSerializer)
                    }
                }
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
                if (!(symbol is PesterSymbolReference pesterSymbol))
                {
                    continue;
                }

                if (_configurationService.CurrentSettings.Pester.UseLegacyCodeLens
                        && pesterSymbol.Command != PesterCommandType.Describe)
                {
                    continue;
                }

                lenses.AddRange(GetPesterLens(pesterSymbol, scriptFile));
            }

            return lenses.ToArray();
        }

        /// <summary>
        /// Resolve the CodeLens provision asynchronously -- just wraps the CodeLens argument in a task.
        /// </summary>
        /// <param name="codeLens">The code lens to resolve.</param>
        /// <param name="scriptFile">The script file.</param>
        /// <returns>The given CodeLens, wrapped in a task.</returns>
        public CodeLens ResolveCodeLens(CodeLens codeLens, ScriptFile scriptFile)
        {
            // This provider has no specific behavior for
            // resolving CodeLenses.
            return codeLens;
        }
    }
}
