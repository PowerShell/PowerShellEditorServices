//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getCommand")]
    public interface IGetCommandHandler : IJsonRpcRequestHandler<GetCommandParams, List<PSCommandMessage>> { }

    public class GetCommandParams : IRequest<List<PSCommandMessage>> { }

    /// <summary>
    /// Describes the message to get the details for a single PowerShell Command
    /// from the current session
    /// </summary>
    public class PSCommandMessage
    {
        public string Name { get; set; }
        public string ModuleName { get; set; }
        public string DefaultParameterSet { get; set; }
        public Dictionary<string, ParameterMetadata> Parameters { get; set; }
        public System.Collections.ObjectModel.ReadOnlyCollection<CommandParameterSetInfo> ParameterSets { get; set; }
    }

    public class GetCommandHandler : IGetCommandHandler
    {
        private readonly ILogger<GetCommandHandler> _logger;
        private readonly PowerShellContextService _powerShellContextService;

        public GetCommandHandler(ILoggerFactory factory, PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<GetCommandHandler>();
            _powerShellContextService = powerShellContextService;
        }

        public async Task<List<PSCommandMessage>> Handle(GetCommandParams request, CancellationToken cancellationToken)
        {
            PSCommand psCommand = new PSCommand();

            // Executes the following:
            // Get-Command -CommandType Function,Cmdlet,ExternalScript | Select-Object -Property Name,ModuleName | Sort-Object -Property Name
            psCommand
                .AddCommand("Microsoft.PowerShell.Core\\Get-Command")
                    .AddParameter("CommandType", new[] { "Function", "Cmdlet", "ExternalScript" })
                .AddCommand("Microsoft.PowerShell.Utility\\Select-Object")
                    .AddParameter("Property", new[] { "Name", "ModuleName" })
                .AddCommand("Microsoft.PowerShell.Utility\\Sort-Object")
                    .AddParameter("Property", "Name");

            IEnumerable<PSObject> result = await _powerShellContextService.ExecuteCommandAsync<PSObject>(psCommand);

            var commandList = new List<PSCommandMessage>();
            if (result != null)
            {
                foreach (dynamic command in result)
                {
                    commandList.Add(new PSCommandMessage
                    {
                        Name = command.Name,
                        ModuleName = command.ModuleName,
                        Parameters = command.Parameters,
                        ParameterSets = command.ParameterSets,
                        DefaultParameterSet = command.DefaultParameterSet
                    });
                }
            }

            return commandList;
        }
    }
}
