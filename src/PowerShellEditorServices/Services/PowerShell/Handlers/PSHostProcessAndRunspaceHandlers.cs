// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    using System.Management.Automation;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
    using OmniSharp.Extensions.JsonRpc;

    internal class PSHostProcessAndRunspaceHandlers : IGetPSHostProcessesHandler, IGetRunspaceHandler
    {
        private readonly ILogger<PSHostProcessAndRunspaceHandlers> _logger;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly IRunspaceContext _runspaceContext;
        private static readonly int s_currentPID = System.Diagnostics.Process.GetCurrentProcess().Id;

        public PSHostProcessAndRunspaceHandlers(
            ILoggerFactory factory,
            IInternalPowerShellExecutionService executionService,
            IRunspaceContext runspaceContext)
        {
            _logger = factory.CreateLogger<PSHostProcessAndRunspaceHandlers>();
            _executionService = executionService;
            _runspaceContext = runspaceContext;
        }

        public async Task<PSHostProcessResponse[]> Handle(GetPSHostProcessesParams request, CancellationToken cancellationToken)
        {
            PSCommand psCommand = new PSCommand().AddCommand(@"Microsoft.PowerShell.Core\Get-PSHostProcessInfo");
            IReadOnlyList<PSObject> processes = await _executionService.ExecutePSCommandAsync<PSObject>(
                psCommand, cancellationToken).ConfigureAwait(false);

            List<PSHostProcessResponse> psHostProcesses = [];
            foreach (dynamic p in processes)
            {
                PSHostProcessResponse response = new()
                {
                    ProcessName = p.ProcessName,
                    ProcessId = p.ProcessId,
                    AppDomainName = p.AppDomainName,
                    MainWindowTitle = p.MainWindowTitle
                };

                // NOTE: We do not currently support attaching to ourself in this manner, so we
                // exclude our process. When we maybe eventually do, we should name it.
                if (response.ProcessId == s_currentPID)
                {
                    continue;
                }

                psHostProcesses.Add(response);
            }

            return psHostProcesses.ToArray();
        }

        public async Task<RunspaceResponse[]> Handle(GetRunspaceParams request, CancellationToken cancellationToken)
        {
            if (request.ProcessId == s_currentPID)
            {
                throw new RpcErrorException(0, null, $"Attaching to the Extension Terminal is not supported!");
            }

            // Create a remote runspace that we will invoke Get-Runspace in.
            IReadOnlyList<PSObject> runspaces = [];
            using (Runspace runspace = RunspaceFactory.CreateRunspace(new NamedPipeConnectionInfo(request.ProcessId)))
            {
                using PowerShell pwsh = PowerShell.Create();
                runspace.Open();
                pwsh.Runspace = runspace;
                // Returns deserialized Runspaces. For simpler code, we use PSObject and rely on dynamic later.
                runspaces = pwsh.AddCommand(@"Microsoft.PowerShell.Utility\Get-Runspace").Invoke<PSObject>();
            }

            List<RunspaceResponse> runspaceResponses = [];
            foreach (dynamic runspace in runspaces)
            {
                // This is the special runspace used for debugging, we can't attach to it.
                if (runspace.Name == "PSAttachRunspace")
                {
                    continue;
                }

                runspaceResponses.Add(
                    new RunspaceResponse
                    {
                        Id = runspace.Id,
                        Name = runspace.Name,
                        Availability = runspace.RunspaceAvailability.ToString()
                    });
            }

            return runspaceResponses.ToArray();
        }
    }
}
