//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

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

        internal SymbolDetails(
            SymbolReference symbolReference, 
            Runspace runspace)
        {
            this.SymbolReference = symbolReference;

            // If the symbol is a command, get its documentation
            if (symbolReference.SymbolType == SymbolType.Function)
            {
                CommandInfo commandInfo =
                    CommandHelpers.GetCommandInfo(
                        symbolReference.SymbolName,
                        runspace);

                if (commandInfo != null)
                {
                    this.Documentation =
                        CommandHelpers.GetCommandSynopsis(
                            commandInfo,
                            runspace);

                    if (commandInfo.CommandType == CommandTypes.Application)
                    {
                        this.DisplayString = "(application) " + symbolReference.SymbolName;
                    }
                    else
                    {
                        this.DisplayString = "function " + symbolReference.SymbolName;
                    }
                }
                else
                {
                    // Command information can't be loaded.  This is likely due to
                    // the symbol being a function that is defined in a file that
                    // hasn't been loaded in the runspace yet.
                    this.DisplayString = "function " + symbolReference.SymbolName;
                }
            }
            else if (symbolReference.SymbolType == SymbolType.Parameter)
            {
                // TODO: Get parameter help
                this.DisplayString = "(parameter) " + symbolReference.SymbolName;
            }
            else if (symbolReference.SymbolType == SymbolType.Variable)
            {
                this.DisplayString = symbolReference.SymbolName;
            }
        }

        #endregion
    }
}
