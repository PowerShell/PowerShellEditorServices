// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Provides detailed information for a given symbol.
    /// TODO: Get rid of this and just use return documentation.
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
        /// Gets the documentation string for this symbol.  Returns an
        /// empty string if the symbol has no documentation.
        /// </summary>
        public StringOrMarkupContent Documentation { get; private set; }

        #endregion

        #region Constructors

        internal static async Task<SymbolDetails> CreateAsync(
            SymbolReference symbolReference,
            IRunspaceInfo currentRunspace,
            IInternalPowerShellExecutionService executionService,
            CancellationToken cancellationToken)
        {
            SymbolDetails symbolDetails = new()
            {
                SymbolReference = symbolReference
            };

            if (symbolReference.Type is SymbolType.Function)
            {
                CommandInfo commandInfo = await CommandHelpers.GetCommandInfoAsync(
                    symbolReference.Id,
                    currentRunspace,
                    executionService,
                    cancellationToken).ConfigureAwait(false);

                if (commandInfo is not null)
                {
                    symbolDetails.Documentation =
                        (await CommandHelpers.GetCommandHelpAsync(
                            commandInfo,
                            executionService,
                            cancellationToken).ConfigureAwait(false)).ToMarkupContent(noTitle: true);

                    if (commandInfo.CommandType == CommandTypes.Application)
                    {
                        symbolDetails.SymbolReference = symbolReference with { Name = $"(application) ${symbolReference.Name}" };
                    }
                }
            }

            return symbolDetails;
        }

        #endregion
    }
}
