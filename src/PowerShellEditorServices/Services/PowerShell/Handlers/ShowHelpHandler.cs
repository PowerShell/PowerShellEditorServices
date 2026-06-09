// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/showHelp", Direction.ClientToServer)]
    internal interface IShowHelpHandler : IJsonRpcRequestHandler<ShowHelpParams, ShowHelpResult> { }

    internal class ShowHelpParams : IRequest<ShowHelpResult>
    {
        public string Text { get; set; }
    }

    internal class ShowHelpResult
    {
        public string HelpText { get; set; }
    }

    internal class ShowHelpHandler : IShowHelpHandler
    {
        private readonly IInternalPowerShellExecutionService _executionService;

        public ShowHelpHandler(IInternalPowerShellExecutionService executionService) => _executionService = executionService;

        public async Task<ShowHelpResult> Handle(ShowHelpParams request, CancellationToken cancellationToken)
        {
            // Resolves the command and returns its full help as a string so the
            // client can display it (e.g. in an editor pane) or pass it to a
            // language model tool. Returns a friendly message if the command
            // cannot be found rather than throwing.
            const string GetHelpScript = @"
                [System.Diagnostics.DebuggerHidden()]
                [CmdletBinding()]
                param (
                    [String]$CommandName
                )
                $command = Microsoft.PowerShell.Core\Get-Command $CommandName -ErrorAction Ignore
                if ($null -eq $command) {
                    return ""No command named '$CommandName' was found.""
                }
                return (Microsoft.PowerShell.Core\Get-Help $CommandName -Full | Microsoft.PowerShell.Utility\Out-String)
                ";

            string commandName = request.Text;
            if (string.IsNullOrEmpty(commandName)) { commandName = "Get-Help"; }

            PSCommand getHelpCommand = new PSCommand()
                .AddScript(GetHelpScript, useLocalScope: true)
                .AddArgument(commandName);

            IReadOnlyList<string> results = await _executionService.ExecutePSCommandAsync<string>(
                getHelpCommand,
                cancellationToken,
                new PowerShellExecutionOptions
                {
                    ThrowOnError = false
                }).ConfigureAwait(false);

            // Get-Help piped through Out-String is padded with a leading blank
            // line and trailing blank lines (a console-formatter artifact); trim
            // both so the help pane and the language model tool get clean output.
            string helpText = results is { Count: > 0 }
                ? string.Concat(results).Trim()
                : string.Empty;

            return new ShowHelpResult { HelpText = helpText };
        }
    }
}
