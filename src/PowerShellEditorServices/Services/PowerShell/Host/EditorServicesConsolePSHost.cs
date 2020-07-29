using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using System;
using System.Globalization;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    using PowerShellContext = Execution.PowerShellContext;

    internal class EditorServicesConsolePSHost : PSHost, IHostSupportsInteractiveSession
    {
        private readonly ILogger _logger;

        private PowerShellContext _pwshContext;

        private Runspace _pushedRunspace;

        public EditorServicesConsolePSHost(
            ILoggerFactory loggerFactory,
            string name,
            Version version,
            PSHost internalHost,
            ConsoleReadLine readline)
        {
            _logger = loggerFactory.CreateLogger<EditorServicesConsolePSHost>();
            _pushedRunspace = null;
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

        public Runspace Runspace => _pwshContext.CurrentPowerShell.Runspace;

        public bool IsRunspacePushed => _pwshContext.PowerShellDepth > 1;

        public override void EnterNestedPrompt()
        {
            _pwshContext.PushNestedPowerShell();
        }

        public override void ExitNestedPrompt()
        {
            _pwshContext.SetShouldExit();
        }

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }

        public void PushRunspace(Runspace runspace)
        {
            _pwshContext.PushPowerShell(runspace);
        }

        public void PopRunspace()
        {
            _pwshContext.SetShouldExit();
        }

        public override void SetShouldExit(int exitCode)
        {
            _pwshContext.SetShouldExit();
        }

        internal void RegisterPowerShellContext(PowerShellContext pwshContext)
        {
            _pwshContext = pwshContext;
        }
    }
}
