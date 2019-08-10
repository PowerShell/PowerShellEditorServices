using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    public class PSHostProcessAndRunspaceHandlers : IGetPSHostProcessesHandler, IGetRunspaceHandler
    {
        private readonly ILogger<GetVersionHandler> _logger;

        public PSHostProcessAndRunspaceHandlers(ILoggerFactory factory)
        {
            _logger = factory.CreateLogger<GetVersionHandler>();
        }

        public Task<PSHostProcessResponse[]> Handle(GetPSHostProcesssesParams request, CancellationToken cancellationToken)
        {
            var psHostProcesses = new List<PSHostProcessResponse>();

            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

            using (var pwsh = PowerShell.Create())
            {
                pwsh.AddCommand("Get-PSHostProcessInfo")
                    .AddCommand("Where-Object")
                        .AddParameter("Property", "ProcessId")
                        .AddParameter("NE")
                        .AddParameter("Value", processId.ToString());

                var processes = pwsh.Invoke<PSObject>();

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

        public Task<RunspaceResponse[]> Handle(GetRunspaceParams request, CancellationToken cancellationToken)
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
                using (var rs = RunspaceFactory.CreateRunspace(new NamedPipeConnectionInfo(pid)))
                using (var ps = PowerShell.Create())
                {
                    rs.Open();
                    ps.Runspace = rs;
                    // Returns deserialized Runspaces. For simpler code, we use PSObject and rely on dynamic later.
                    runspaces = ps.AddCommand("Microsoft.PowerShell.Utility\\Get-Runspace").Invoke<PSObject>();
                }
            }
            else
            {
                // TODO: Bring back
                // var psCommand = new PSCommand().AddCommand("Microsoft.PowerShell.Utility\\Get-Runspace");
                // var sb = new StringBuilder();
                // // returns (not deserialized) Runspaces. For simpler code, we use PSObject and rely on dynamic later.
                // runspaces = await editorSession.PowerShellContext.ExecuteCommandAsync<PSObject>(psCommand, sb);
            }

            var runspaceResponses = new List<RunspaceResponse>();

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

            return Task.FromResult(runspaceResponses.ToArray());
        }
    }
}
