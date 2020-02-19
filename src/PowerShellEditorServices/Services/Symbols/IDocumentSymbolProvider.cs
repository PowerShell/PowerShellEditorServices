//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
        IEnumerable<ISymbolReference> ProvideDocumentSymbols(ScriptFile scriptFile);
    }
}
