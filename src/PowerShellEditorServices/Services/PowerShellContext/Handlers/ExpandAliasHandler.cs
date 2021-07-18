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
        private readonly PowerShellContextService _powerShellContextService;

        public ExpandAliasHandler(ILoggerFactory factory, PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<ExpandAliasHandler>();
            _powerShellContextService = powerShellContextService;
        }

        public async Task<ExpandAliasResult> Handle(ExpandAliasParams request, CancellationToken cancellationToken)
        {

            var psCommand = new PSCommand();
            psCommand
                .AddStatement()
                .AddCommand("Get-EditorServicesParserAst")
                .AddParameter("ScriptBlock", request.Text)
                .AddParameter("CommandType", CommandTypes.Alias)
                .AddParameter("PSTokenType", PSTokenType.Command);
            var result = await _powerShellContextService.ExecuteCommandAsync<string>(psCommand).ConfigureAwait(false);

            return new ExpandAliasResult
            {
                Text = result.First()
            };
        }
    }
}
