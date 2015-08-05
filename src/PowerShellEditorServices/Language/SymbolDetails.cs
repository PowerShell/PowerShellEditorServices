//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Language
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    /// <summary>
    /// Provides detailed information for a given symbol.
    /// </summary>
    public class SymbolDetails
    {
        #region Properties

        /// <summary>
        /// Gets the name of the symbol.
        /// </summary>
        public string Name { get; private set; }

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

        internal SymbolDetails()
        {
        }

        internal SymbolDetails(
            SymbolReference symbolReference, 
            Runspace runspace)
        {
            this.Name = symbolReference.SymbolName;

            // If the symbol is a command, get its documentation
            if (symbolReference.SymbolType == SymbolType.Function)
            {
                CommandInfo commandInfo =
                    GetCommandInfo(
                        symbolReference.SymbolName,
                        runspace);

                this.Documentation =
                    GetCommandSynopsis(
                        commandInfo, 
                        runspace);
            }
        }
        private static CommandInfo GetCommandInfo(
            string commandName, 
            Runspace runspace)
        {
            CommandInfo commandInfo = null;

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.Runspace = runspace;
                powerShell.AddCommand("Get-Command");
                powerShell.AddArgument(commandName);
                commandInfo = powerShell.Invoke<CommandInfo>().FirstOrDefault();
            }

            return commandInfo;
        }

        private static string GetCommandSynopsis(
            CommandInfo commandInfo, 
            Runspace runspace)
        {
            string synopsisString = string.Empty;

            PSObject helpObject = null;

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.Runspace = runspace;
                powerShell.AddCommand("Get-Help");
                powerShell.AddArgument(commandInfo);
                helpObject = powerShell.Invoke<PSObject>().FirstOrDefault();
            }

            // Extract the synopsis string from the object
            synopsisString = 
                (string)helpObject.Properties["synopsis"].Value ?? 
                string.Empty;

            return synopsisString;
        }

        #endregion
    }
}
