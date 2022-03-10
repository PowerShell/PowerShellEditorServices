// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class GetVersionHandler : IGetVersionHandler
    {
        private readonly ILogger<GetVersionHandler> _logger;
        private IRunspaceContext _runspaceContext;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly ILanguageServerFacade _languageServer;
        private readonly ConfigurationService _configurationService;

        public GetVersionHandler(
            ILoggerFactory factory,
            IRunspaceContext runspaceContext,
            IInternalPowerShellExecutionService executionService,
            ILanguageServerFacade languageServer,
            ConfigurationService configurationService)
        {
            _logger = factory.CreateLogger<GetVersionHandler>();
            _runspaceContext = runspaceContext;
            _executionService = executionService;
            _languageServer = languageServer;
            _configurationService = configurationService;
        }

        public async Task<PowerShellVersion> Handle(GetVersionParams request, CancellationToken cancellationToken)
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

            return new PowerShellVersion
            {
                Version = VersionUtils.PSVersionString,
                Edition = VersionUtils.PSEdition,
                DisplayVersion = VersionUtils.PSVersion.ToString(2),
                Architecture = architecture.ToString()
            };
        }

        private enum PowerShellProcessArchitecture
        {
            Unknown,
            X86,
            X64
        }
    }
}
