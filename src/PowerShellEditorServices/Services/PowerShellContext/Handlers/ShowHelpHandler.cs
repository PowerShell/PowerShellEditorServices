//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/showHelp")]
    internal interface IShowHelpHandler : IJsonRpcNotificationHandler<ShowHelpParams> { }

    internal class ShowHelpParams : IRequest
    {
        public string Text { get; set; }
    }

    internal class ShowHelpHandler : IShowHelpHandler
    {
        private readonly ILogger _logger;
        private readonly PowerShellContextService _powerShellContextService;

        public ShowHelpHandler(ILoggerFactory factory, PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<ShowHelpHandler>();
            _powerShellContextService = powerShellContextService;
        }

        public async Task<Unit> Handle(ShowHelpParams request, CancellationToken cancellationToken)
        {
            const string CheckHelpScript = @"
                [CmdletBinding()]
                param (
                    [String]$CommandName
                )
                try {
                    $command = Microsoft.PowerShell.Core\Get-Command $CommandName -ErrorAction Stop
                } catch [System.Management.Automation.CommandNotFoundException] {
                    $PSCmdlet.ThrowTerminatingError($PSItem)
                }
                try {
                    $helpUri = [Microsoft.PowerShell.Commands.GetHelpCodeMethods]::GetHelpUri($command)

                    $oldSslVersion = [System.Net.ServicePointManager]::SecurityProtocol
                    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

                    # HEAD means we don't need the content itself back, just the response header
                    $status = (Microsoft.PowerShell.Utility\Invoke-WebRequest -Method Head -Uri $helpUri -TimeoutSec 5 -ErrorAction Stop).StatusCode
                    if ($status -lt 400) {
                        $null = Microsoft.PowerShell.Core\Get-Help $CommandName -Online
                        return
                    }
                } catch {
                    # Ignore - we want to drop out to Get-Help -Full
                } finally {
                    [System.Net.ServicePointManager]::SecurityProtocol = $oldSslVersion
                }

                return Microsoft.PowerShell.Core\Get-Help $CommandName -Full
                ";

            string helpParams = request.Text;
            if (string.IsNullOrEmpty(helpParams)) { helpParams = "Get-Help"; }

            PSCommand checkHelpPSCommand = new PSCommand()
                .AddScript(CheckHelpScript, useLocalScope: true)
                .AddArgument(helpParams);

            // TODO: Rather than print the help in the console, we should send the string back
            //       to VSCode to display in a help pop-up (or similar)
            await _powerShellContextService.ExecuteCommandAsync<PSObject>(checkHelpPSCommand, sendOutputToHost: true).ConfigureAwait(false);
            return Unit.Value;
        }
    }
}
