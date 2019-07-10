using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;

namespace PowerShellEditorServices.Engine.Services.Workspace.Handlers
{
    public class GetVersionHandler : IGetVersionHandler
    {
        private readonly ILogger<GetVersionHandler> _logger;

        public GetVersionHandler(ILoggerFactory factory)
        {
            _logger = factory.CreateLogger<GetVersionHandler>();
        }

        public Task<PowerShellVersionDetails> Handle(GetVersionParams request, CancellationToken cancellationToken)
        {
            var architecture = PowerShellProcessArchitecture.Unknown;
            // This should be changed to using a .NET call sometime in the future... but it's just for logging purposes.
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            if (arch != null)
            {
                if (string.Equals(arch, "AMD64", StringComparison.CurrentCultureIgnoreCase))
                {
                    architecture = PowerShellProcessArchitecture.X64;
                }
                else if (string.Equals(arch, "x86", StringComparison.CurrentCultureIgnoreCase))
                {
                    architecture = PowerShellProcessArchitecture.X86;
                }
            }

            return Task.FromResult(new PowerShellVersionDetails {
                Version = VersionUtils.PSVersion.ToString(),
                Edition = VersionUtils.PSEdition,
                DisplayVersion = VersionUtils.PSVersion.ToString(2),
                Architecture = architecture.ToString()
            });
        }

        private enum PowerShellProcessArchitecture
        {
            Unknown,
            X86,
            X64
        }
    }
}
