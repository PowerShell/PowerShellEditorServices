// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
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

        IEnumerable<SymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile) => scriptFile.References.GetAllReferences();
    }
}
