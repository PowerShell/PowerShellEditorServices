using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using System;
using System.Globalization;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHost : PSHost, IHostSupportsInteractiveSession
    {
        private readonly ILogger _logger;

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

        public bool IsRunspacePushed { get; private set; }

        public Runspace Runspace { get; private set; }

        public override void EnterNestedPrompt()
        {
            throw new NotImplementedException();
        }

        public override void ExitNestedPrompt()
        {
            throw new NotImplementedException();
        }

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }

        public void PopRunspace()
        {
            Runspace = _pushedRunspace;
            _pushedRunspace = null;
            IsRunspacePushed = false;
        }

        public void PushRunspace(Runspace runspace)
        {
            _pushedRunspace = Runspace;
            Runspace = runspace;
            IsRunspacePushed = true;
        }

        public override void SetShouldExit(int exitCode)
        {
            if (IsRunspacePushed)
            {
                PopRunspace();
            }
        }

        public void RegisterRunspace(Runspace runspace)
        {
            Runspace = runspace;
        }
    }
}
