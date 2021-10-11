// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class GetVersionHandler : IGetVersionHandler
    {
        private static readonly Version s_desiredPackageManagementVersion = new Version(1, 4, 6);

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

            if (VersionUtils.IsPS5 && _configurationService.CurrentSettings.PromptToUpdatePackageManagement)
            {
                await CheckPackageManagement().ConfigureAwait(false);
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

        private async Task CheckPackageManagement()
        {
            PSCommand getModule = new PSCommand().AddCommand("Get-Module").AddParameter("ListAvailable").AddParameter("Name", "PackageManagement");
            foreach (PSModuleInfo module in await _executionService.ExecutePSCommandAsync<PSModuleInfo>(getModule, CancellationToken.None).ConfigureAwait(false))
            {
                // The user has a good enough version of PackageManagement
                if (module.Version >= s_desiredPackageManagementVersion)
                {
                    break;
                }

                _logger.LogDebug("Old version of PackageManagement detected.");

                if (_runspaceContext.CurrentRunspace.Runspace.SessionStateProxy.LanguageMode != PSLanguageMode.FullLanguage)
                {
                    _languageServer.Window.ShowWarning("You have an older version of PackageManagement known to cause issues with the PowerShell extension. Please run the following command in a new Windows PowerShell session and then restart the PowerShell extension: `Install-Module PackageManagement -Force -AllowClobber -MinimumVersion 1.4.6`");
                    return;
                }

                var takeActionText = "Yes";
                MessageActionItem messageAction = await _languageServer.Window.ShowMessageRequest(new ShowMessageRequestParams
                {
                    Message = "You have an older version of PackageManagement known to cause issues with the PowerShell extension. Would you like to update PackageManagement (You will need to restart the PowerShell extension after)?",
                    Type = MessageType.Warning,
                    Actions = new[]
                    {
                        new MessageActionItem
                        {
                            Title = takeActionText
                        },
                        new MessageActionItem
                        {
                            Title = "Not now"
                        }
                    }
                })
                .ConfigureAwait(false);

                // If the user chose "Not now" ignore it for the rest of the session.
                if (messageAction?.Title == takeActionText)
                {
                    var command = new PSCommand().AddScript("powershell.exe -NoLogo -NoProfile -Command 'Install-Module -Name PackageManagement -Force -MinimumVersion 1.4.6 -Scope CurrentUser -AllowClobber'");

                    await _executionService.ExecutePSCommandAsync(
                        command,
                        CancellationToken.None,
                        new PowerShellExecutionOptions { WriteInputToHost = true, WriteOutputToHost = true, AddToHistory = true, ThrowOnError = false }).ConfigureAwait(false);

                    // TODO: Error handling here

                    /*
                    if (errors.Length == 0)
                    {
                        _logger.LogDebug("PackageManagement is updated.");
                        _languageServer.Window.ShowMessage(new ShowMessageParams
                        {
                            Type = MessageType.Info,
                            Message = "PackageManagement updated, If you already had PackageManagement loaded in your session, please restart the PowerShell extension."
                        });
                    }
                    else
                    {
                        // There were errors installing PackageManagement.
                        _logger.LogError($"PackageManagement installation had errors: {errors.ToString()}");
                        _languageServer.Window.ShowMessage(new ShowMessageParams
                        {
                            Type = MessageType.Error,
                            Message = "PackageManagement update failed. This might be due to PowerShell Gallery using TLS 1.2. More info can be found at https://aka.ms/psgallerytls"
                        });
                    }
                    */
                }
            }
        }
    }
}
