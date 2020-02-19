//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Provides detailed information for a given symbol.
    /// </summary>
    [DebuggerDisplay("SymbolReference = {SymbolReference.SymbolType}/{SymbolReference.SymbolName}, DisplayString = {DisplayString}")]
    internal class SymbolDetails
    {
        #region Properties

        /// <summary>
        /// Gets the original symbol reference which was used to gather details.
        /// </summary>
        public SymbolReference SymbolReference { get; private set; }

        /// <summary>
        /// Gets the display string for this symbol.
        /// </summary>
        public string DisplayString { get; private set; }

        /// <summary>
        /// Gets the documentation string for this symbol.  Returns an
        /// empty string if the symbol has no documentation.
        /// </summary>
        public string Documentation { get; private set; }

        #endregion

        #region Constructors

        static internal async Task<SymbolDetails> CreateAsync(
            SymbolReference symbolReference,
            PowerShellContextService powerShellContext)
        {
            SymbolDetails symbolDetails = new SymbolDetails
            {
                SymbolReference = symbolReference
            };

            switch (symbolReference.SymbolType)
            {
                case SymbolType.Function:
                    CommandInfo commandInfo = await CommandHelpers.GetCommandInfoAsync(
                        symbolReference.SymbolName,
                        powerShellContext).ConfigureAwait(false);

                    if (commandInfo != null)
                    {
                        symbolDetails.Documentation =
                            await CommandHelpers.GetCommandSynopsisAsync(
                                commandInfo,
                                powerShellContext).ConfigureAwait(false);

                        if (commandInfo.CommandType == CommandTypes.Application)
                        {
                            symbolDetails.DisplayString = "(application) " + symbolReference.SymbolName;
                            return symbolDetails;
                        }
                    }

                    symbolDetails.DisplayString = "function " + symbolReference.SymbolName;
                    return symbolDetails;

                case SymbolType.Parameter:
                    // TODO: Get parameter help
                    symbolDetails.DisplayString = "(parameter) " + symbolReference.SymbolName;
                    return symbolDetails;

                case SymbolType.Variable:
                    symbolDetails.DisplayString = symbolReference.SymbolName;
                    return symbolDetails;

                default:
                    return symbolDetails;
            }
        }

        #endregion
    }
}
