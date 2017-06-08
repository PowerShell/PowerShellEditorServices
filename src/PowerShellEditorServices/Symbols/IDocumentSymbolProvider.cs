//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Symbols
{
    /// <summary>
    /// Specifies the contract for a document symbols provider.
    /// </summary>
    public interface IDocumentSymbolProvider : IFeatureProvider
    {
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
