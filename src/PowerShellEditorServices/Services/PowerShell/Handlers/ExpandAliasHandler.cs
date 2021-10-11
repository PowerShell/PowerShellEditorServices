// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/expandAlias")]
    internal interface IExpandAliasHandler : IJsonRpcRequestHandler<ExpandAliasParams, ExpandAliasResult> { }

    internal class ExpandAliasParams : IRequest<ExpandAliasResult>
    {
        public string Text { get; set; }
    }

    internal class ExpandAliasResult
    {
        public string Text { get; set; }
    }

    internal class ExpandAliasHandler : IExpandAliasHandler
    {
        private readonly ILogger _logger;
        private readonly IInternalPowerShellExecutionService _executionService;

        public ExpandAliasHandler(ILoggerFactory factory, IInternalPowerShellExecutionService executionService)
        {
            _logger = factory.CreateLogger<ExpandAliasHandler>();
            _executionService = executionService;
        }

        public async Task<ExpandAliasResult> Handle(ExpandAliasParams request, CancellationToken cancellationToken)
        {
            const string script = @"
function __Expand-Alias {

    param($targetScript)

    [ref]$errors=$null

    $tokens = [System.Management.Automation.PsParser]::Tokenize($targetScript, $errors).Where({$_.type -eq 'command'}) |
                    Sort-Object Start -Descending

    foreach ($token in  $tokens) {
        $definition=(Get-Command ('`'+$token.Content) -CommandType Alias -ErrorAction SilentlyContinue).Definition

        if($definition) {
            $lhs=$targetScript.Substring(0, $token.Start)
            $rhs=$targetScript.Substring($token.Start + $token.Length)

            $targetScript=$lhs + $definition + $rhs
       }
    }

    $targetScript
}";

            // TODO: Refactor to not rerun the function definition every time.
            var psCommand = new PSCommand();
            psCommand
                .AddScript(script)
                .AddStatement()
                .AddCommand("__Expand-Alias")
                .AddArgument(request.Text);
            var result = await _executionService.ExecutePSCommandAsync<string>(psCommand, cancellationToken).ConfigureAwait(false);

            return new ExpandAliasResult
            {
                Text = result.First()
            };
        }
    }
}
