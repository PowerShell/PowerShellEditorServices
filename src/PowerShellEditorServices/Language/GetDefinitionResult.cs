//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// A class to contain the found defintion of a symbol.
    /// It contains the symbol reference of the defintion
    /// </summary>
    public class GetDefinitionResult
    {
        #region Properties
        /// <summary>
        /// Gets the symbolReference of the found definition
        /// </summary>
        public SymbolReference FoundDefinition { get; internal set; }
        #endregion

        /// <summary>
        /// Constructs an instance of a GetDefinitionResut
        /// </summary>
        /// <param name="symRef">The symbolRefernece for the found definition</param>
        public GetDefinitionResult(SymbolReference symRef)
        {
            FoundDefinition = symRef;
        }
    }
}
