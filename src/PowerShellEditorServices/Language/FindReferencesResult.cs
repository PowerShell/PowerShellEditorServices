//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Language
{
    /// <summary>
    /// A class to contain the found references of a symbol.
    /// It contains a collection of symbol references, the symbol name, and the symbol's file offset
    /// </summary>
    public class FindReferencesResult
    {
        #region Properties
        /// <summary>
        /// Gets the name of the symbol
        /// </summary>
        public string SymbolName { get; internal set; }
        
        /// <summary>
        /// Gets the file offset (location based on line and column number) of the symbol
        /// </summary>
        public int SymbolFileOffset { get; internal set; }

        /// <summary>
        /// Gets the collection of SymboleReferences for the all references to the symbol 
        /// </summary>
        public IEnumerable<SymbolReference> FoundReferences { get; internal set; }
        #endregion
    }
}
