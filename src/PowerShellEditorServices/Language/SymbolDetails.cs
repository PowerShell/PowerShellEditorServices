﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides detailed information for a given symbol.
    /// </summary>
    [DebuggerDisplay("SymbolReference = {SymbolReference.SymbolType}/{SymbolReference.SymbolName}, DisplayString = {DisplayString}")]
    public class SymbolDetails
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

        static internal async Task<SymbolDetails> Create(
            SymbolReference symbolReference, 
            PowerShellContext powerShellContext)
        {
            SymbolDetails symbolDetails = new SymbolDetails();
            symbolDetails.SymbolReference = symbolReference;

            // If the symbol is a command, get its documentation
            if (symbolReference.SymbolType == SymbolType.Function)
            {
                CommandInfo commandInfo =
                    await CommandHelpers.GetCommandInfo(
                        symbolReference.SymbolName,
                        powerShellContext);

                if (commandInfo != null)
                {
                    symbolDetails.Documentation =
                        await CommandHelpers.GetCommandSynopsis(
                            commandInfo,
                            powerShellContext);

                    if (commandInfo.CommandType == CommandTypes.Application)
                    {
                        symbolDetails.DisplayString = "(application) " + symbolReference.SymbolName;
                    }
                    else
                    {
                        symbolDetails.DisplayString = "function " + symbolReference.SymbolName;
                    }
                }
                else
                {
                    // Command information can't be loaded.  This is likely due to
                    // the symbol being a function that is defined in a file that
                    // hasn't been loaded in the runspace yet.
                    symbolDetails.DisplayString = "function " + symbolReference.SymbolName;
                }
            }
            else if (symbolReference.SymbolType == SymbolType.Parameter)
            {
                // TODO: Get parameter help
                symbolDetails.DisplayString = "(parameter) " + symbolReference.SymbolName;
            }
            else if (symbolReference.SymbolType == SymbolType.Variable)
            {
                symbolDetails.DisplayString = symbolReference.SymbolName;
            }

            return symbolDetails;
        }

        #endregion
    }
}
