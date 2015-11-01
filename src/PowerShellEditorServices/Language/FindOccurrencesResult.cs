//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// A class for the found occurences of a symbol.
    /// It contains a collection of symbol references. 
    /// </summary>
    public class FindOccurrencesResult
    {
        #region Properties
        /// <summary>
        /// Gets the collection of SymboleReferences for the all occurences of the symbol 
        /// </summary>
        public IEnumerable<SymbolReference> FoundOccurrences { get; internal set; }
        #endregion
    }
}
