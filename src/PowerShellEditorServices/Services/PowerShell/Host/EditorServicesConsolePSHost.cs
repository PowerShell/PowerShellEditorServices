using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using System;
using System.Globalization;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    using System.Management.Automation.Runspaces;

    internal class EditorServicesConsolePSHost : PSHost, IHostSupportsInteractiveSession
    {
        private readonly ILogger _logger;

        private PowerShellContext _psContext;

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

        public Runspace Runspace => _psContext.CurrentRunspace;

        public bool IsRunspacePushed => _psContext.IsRunspacePushed;

        public override void EnterNestedPrompt()
        {
            _psContext.PushNestedPowerShell();
        }

        public override void ExitNestedPrompt()
        {
            _psContext.SetShouldExit(exitCode: null);
        }

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }

        public void PushRunspace(Runspace runspace)
        {
            _psContext.PushPowerShell(runspace);
        }

        public void PopRunspace()
        {
            _psContext.SetShouldExit(exitCode: null);
        }

        public override void SetShouldExit(int exitCode)
        {
            _psContext.SetShouldExit(exitCode);
        }

        internal void RegisterPowerShellContext(PowerShellContext psContext)
        {
            _psContext = psContext;
        }
    }
}
