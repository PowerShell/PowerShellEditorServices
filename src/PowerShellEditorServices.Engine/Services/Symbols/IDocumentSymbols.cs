//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.PowerShell.EditorServices.Engine.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Engine.Services.Symbols
{
    /// <summary>
    /// Specifies the contract for an implementation of
    /// the IDocumentSymbols component.
    /// </summary>
    public interface IDocumentSymbols
    {
        /// <summary>
        /// Gets the collection of IDocumentSymbolsProvider implementations
        /// that are registered with this component.
        /// </summary>
        Collection<IDocumentSymbolProvider> Providers { get; }

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
