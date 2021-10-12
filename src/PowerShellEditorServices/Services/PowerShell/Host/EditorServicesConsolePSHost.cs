using System;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    public class EditorServicesConsolePSHost : PSHost, IHostSupportsInteractiveSession
    {
        private readonly PsesInternalHost _internalHost;

        private readonly Lazy<PSObject> _privateDataLazy;

        internal EditorServicesConsolePSHost(
            PsesInternalHost internalHost)
        {
            _internalHost = internalHost;
            _privateDataLazy = new Lazy<PSObject>(CreateConsoleColorProxyForPrivateData);
        }

        public override CultureInfo CurrentCulture => _internalHost.CurrentCulture;

        public override CultureInfo CurrentUICulture => _internalHost.CurrentUICulture;

        public override Guid InstanceId => _internalHost.InstanceId;

        public override string Name => _internalHost.Name;

        public override PSHostUserInterface UI => _internalHost.UI;

        public override Version Version => _internalHost.Version;

        public bool IsRunspacePushed => _internalHost.IsRunspacePushed;

        public System.Management.Automation.Runspaces.Runspace Runspace => _internalHost.Runspace;

        public override PSObject PrivateData => _privateDataLazy.Value;

        public override void EnterNestedPrompt() => _internalHost.EnterNestedPrompt();

        public override void ExitNestedPrompt() => _internalHost.ExitNestedPrompt();

        public override void NotifyBeginApplication() => _internalHost.NotifyBeginApplication();

        public override void NotifyEndApplication() => _internalHost.NotifyEndApplication();

        public void PopRunspace() => _internalHost.PopRunspace();

        public void PushRunspace(System.Management.Automation.Runspaces.Runspace runspace) => _internalHost.PushRunspace(runspace);

        public override void SetShouldExit(int exitCode) => _internalHost.SetShouldExit(exitCode);

        private PSObject CreateConsoleColorProxyForPrivateData()
        {
            if (UI is null)
            {
                return null;
            }

            return PSObject.AsPSObject(new ConsoleColorProxy((EditorServicesConsolePSHostUserInterface)UI));
        }
    }
}
