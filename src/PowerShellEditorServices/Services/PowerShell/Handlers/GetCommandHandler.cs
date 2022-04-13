// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getCommand", Direction.ClientToServer)]
    internal interface IGetCommandHandler : IJsonRpcRequestHandler<GetCommandParams, List<PSCommandMessage>> { }

    internal class GetCommandParams : IRequest<List<PSCommandMessage>> { }

    /// <summary>
    /// Describes the message to get the details for a single PowerShell Command
    /// from the current session
    /// </summary>
    internal class PSCommandMessage
    {
        public string Name { get; set; }
        public string ModuleName { get; set; }
        public string DefaultParameterSet { get; set; }
        public Dictionary<string, ParameterMetadata> Parameters { get; set; }
        public System.Collections.ObjectModel.ReadOnlyCollection<CommandParameterSetInfo> ParameterSets { get; set; }
    }

    internal class GetCommandHandler : IGetCommandHandler
    {
        private readonly IInternalPowerShellExecutionService _executionService;

        public GetCommandHandler(IInternalPowerShellExecutionService executionService) => _executionService = executionService;

        public async Task<List<PSCommandMessage>> Handle(GetCommandParams request, CancellationToken cancellationToken)
        {
            PSCommand psCommand = new();

            // Executes the following:
            // Get-Command -CommandType Function,Cmdlet,ExternalScript | Sort-Object -Property Name
            psCommand
                .AddCommand("Microsoft.PowerShell.Core\\Get-Command")
                    .AddParameter("CommandType", new[] { "Function", "Cmdlet", "ExternalScript" })
                .AddCommand("Microsoft.PowerShell.Utility\\Sort-Object")
                    .AddParameter("Property", "Name");

            IEnumerable<CommandInfo> result = await _executionService.ExecutePSCommandAsync<CommandInfo>(psCommand, cancellationToken).ConfigureAwait(false);

            List<PSCommandMessage> commandList = new();
            if (result != null)
            {
                foreach (CommandInfo command in result)
                {
                    // Some info objects have a quicker way to get the DefaultParameterSet. These
                    // are also the most likely to show up so win-win.
                    string defaultParameterSet = null;
                    switch (command)
                    {
                        case CmdletInfo info:
                            defaultParameterSet = info.DefaultParameterSet;
                            break;
                        case FunctionInfo info:
                            defaultParameterSet = info.DefaultParameterSet;
                            break;
                    }

                    if (defaultParameterSet == null)
                    {
                        // Try to get the default ParameterSet if it isn't streamlined (ExternalScriptInfo for example)
                        foreach (CommandParameterSetInfo parameterSetInfo in command.ParameterSets)
                        {
                            if (parameterSetInfo.IsDefault)
                            {
                                defaultParameterSet = parameterSetInfo.Name;
                                break;
                            }
                        }
                    }

                    commandList.Add(new PSCommandMessage
                    {
                        Name = command.Name,
                        ModuleName = command.ModuleName,
                        Parameters = command.Parameters,
                        ParameterSets = command.ParameterSets,
                        DefaultParameterSet = defaultParameterSet
                    });
                }
            }

            return commandList;
        }
    }
}
