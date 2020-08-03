using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using System;
using System.Globalization;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using static Microsoft.PowerShell.EditorServices.Services.PowerShell.PowerShellExecutionService;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHost : PSHost, IHostSupportsInteractiveSession
    {
        private readonly ILogger _logger;

        private PowerShellRunspaceContext _psRunspaceContext;

        public EditorServicesConsolePSHost(
            ILoggerFactory loggerFactory,
            string name,
            Version version,
            PSHost internalHost,
            ConsoleReadLine readline)
        {
            _logger = loggerFactory.CreateLogger<EditorServicesConsolePSHost>();
            Name = name;
            Version = version;
            UI = new EditorServicesConsolePSHostUserInterface(loggerFactory, readline, internalHost.UI);
        }

        public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;

        public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;

        public override Guid InstanceId { get; } = Guid.NewGuid();

        public override string Name { get; }

        public override PSHostUserInterface UI { get; }

        public override Version Version { get; }

        public Runspace Runspace => _psRunspaceContext.Runspace;

        public bool IsRunspacePushed => _psRunspaceContext.IsRunspacePushed;

        public override void EnterNestedPrompt()
        {
            _psRunspaceContext.PushNestedPowerShell();
        }

        public override void ExitNestedPrompt()
        {
            _psRunspaceContext.SetShouldExit();
        }

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }

        public void PushRunspace(Runspace runspace)
        {
            _psRunspaceContext.PushPowerShell(runspace);
        }

        public void PopRunspace()
        {
            _psRunspaceContext.SetShouldExit();
        }

        public override void SetShouldExit(int exitCode)
        {
            _psRunspaceContext.SetShouldExit();
        }

        internal void RegisterPowerShellContext(PowerShellExecutionService.PowerShellRunspaceContext psRunspaceContext)
        {
            _psRunspaceContext = psRunspaceContext;
        }
    }
}
