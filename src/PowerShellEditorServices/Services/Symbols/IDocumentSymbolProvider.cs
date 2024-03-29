// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Specifies the contract for a document symbols provider.
    /// </summary>
    internal interface IDocumentSymbolProvider
    {
        string ProviderId { get; }

        /// <summary>
        /// Provides a list of symbols for the given document.
        /// </summary>
        /// <param name="scriptFile">
        /// The document for which SymbolReferences should be provided.
        /// </param>
        /// <returns>An IEnumerable collection of SymbolReferences.</returns>
        IEnumerable<SymbolReference> ProvideDocumentSymbols(ScriptFile scriptFile);
    }
}
