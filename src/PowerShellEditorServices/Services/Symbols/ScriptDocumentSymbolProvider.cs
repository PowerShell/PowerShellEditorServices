// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Provides an IDocumentSymbolProvider implementation for
    /// enumerating symbols in script (.psd1, .psm1) files.
    /// </summary>
    internal class ScriptDocumentSymbolProvider : IDocumentSymbolProvider
    {
        string IDocumentSymbolProvider.ProviderId => nameof(ScriptDocumentSymbolProvider);

        IEnumerable<ISymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            // If we have an AST, then we know it's a PowerShell file
            // so lets try to find symbols in the document.
            return scriptFile?.ScriptAst != null
                ? FindSymbolsInDocument(scriptFile.ScriptAst)
                : Enumerable.Empty<SymbolReference>();
        }

        /// <summary>
        /// Finds all symbols in a script
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <returns>A collection of SymbolReference objects</returns>
        public static IEnumerable<SymbolReference> FindSymbolsInDocument(Ast scriptAst)
        {
            FindSymbolsVisitor findSymbolsVisitor = new();
            scriptAst.Visit(findSymbolsVisitor);
            return findSymbolsVisitor.SymbolReferences;
        }
    }
}
