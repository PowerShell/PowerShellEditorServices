// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    using Microsoft.PowerShell.EditorServices.Services.PowerShell;
    using System.Management.Automation;

    internal class PSHostProcessAndRunspaceHandlers : IGetPSHostProcessesHandler, IGetRunspaceHandler
    {
        private readonly ILogger<PSHostProcessAndRunspaceHandlers> _logger;
        private readonly IInternalPowerShellExecutionService _executionService;

        public PSHostProcessAndRunspaceHandlers(ILoggerFactory factory, IInternalPowerShellExecutionService executionService)
        {
            _logger = factory.CreateLogger<PSHostProcessAndRunspaceHandlers>();
            _executionService = executionService;
        }

        public Task<PSHostProcessResponse[]> Handle(GetPSHostProcesssesParams request, CancellationToken cancellationToken)
        {
            List<PSHostProcessResponse> psHostProcesses = new();

            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

            using (PowerShell pwsh = PowerShell.Create())
            {
                pwsh.AddCommand("Get-PSHostProcessInfo")
                    .AddCommand("Where-Object")
                        .AddParameter("Property", "ProcessId")
                        .AddParameter("NE")
                        .AddParameter("Value", processId.ToString());

                System.Collections.ObjectModel.Collection<PSObject> processes = pwsh.Invoke<PSObject>();

                if (processes != null)
                {
                    foreach (dynamic p in processes)
                    {
                        psHostProcesses.Add(
                            new PSHostProcessResponse
                            {
                                ProcessName = p.ProcessName,
                                ProcessId = p.ProcessId,
                                AppDomainName = p.AppDomainName,
                                MainWindowTitle = p.MainWindowTitle
                            });
                    }
                }
            }

            return Task.FromResult(psHostProcesses.ToArray());
        }

        public async Task<RunspaceResponse[]> Handle(GetRunspaceParams request, CancellationToken cancellationToken)
        {
            IEnumerable<PSObject> runspaces = null;

            if (request.ProcessId == null)
            {
                request.ProcessId = "current";
            }

            // If the processId is a valid int, we need to run Get-Runspace within that process
            // otherwise just use the current runspace.
            if (int.TryParse(request.ProcessId, out int pid))
            {
                // Create a remote runspace that we will invoke Get-Runspace in.
                using Runspace rs = RunspaceFactory.CreateRunspace(new NamedPipeConnectionInfo(pid));
                using PowerShell ps = PowerShell.Create();
                rs.Open();
                ps.Runspace = rs;
                // Returns deserialized Runspaces. For simpler code, we use PSObject and rely on dynamic later.
                runspaces = ps.AddCommand("Microsoft.PowerShell.Utility\\Get-Runspace").Invoke<PSObject>();
            }
            else
            {
                PSCommand psCommand = new PSCommand().AddCommand("Microsoft.PowerShell.Utility\\Get-Runspace");
                // returns (not deserialized) Runspaces. For simpler code, we use PSObject and rely on dynamic later.
                runspaces = await _executionService.ExecutePSCommandAsync<PSObject>(psCommand, cancellationToken).ConfigureAwait(false);
            }

            List<RunspaceResponse> runspaceResponses = new();

            if (runspaces != null)
            {
                foreach (dynamic runspace in runspaces)
                {
                    runspaceResponses.Add(
                        new RunspaceResponse
                        {
                            Id = runspace.Id,
                            Name = runspace.Name,
                            Availability = runspace.RunspaceAvailability.ToString()
                        });
                }
            }

            return runspaceResponses.ToArray();
        }
    }
}
