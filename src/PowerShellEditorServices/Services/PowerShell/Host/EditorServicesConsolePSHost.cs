// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    public class EditorServicesConsolePSHost : PSHost, IHostSupportsInteractiveSession
    {
        private readonly PsesInternalHost _internalHost;

        internal EditorServicesConsolePSHost(
            PsesInternalHost internalHost) => _internalHost = internalHost;

        public override CultureInfo CurrentCulture => _internalHost.CurrentCulture;

        public override CultureInfo CurrentUICulture => _internalHost.CurrentUICulture;

        public override Guid InstanceId => _internalHost.InstanceId;

        public override string Name => _internalHost.Name;

        public override System.Management.Automation.PSObject PrivateData => _internalHost.PrivateData;

        public override PSHostUserInterface UI => _internalHost.UI;

        public override Version Version => _internalHost.Version;

        public bool IsRunspacePushed => _internalHost.IsRunspacePushed;

        public System.Management.Automation.Runspaces.Runspace Runspace => _internalHost.Runspace;

        public override void EnterNestedPrompt() => _internalHost.EnterNestedPrompt();

        public override void ExitNestedPrompt() => _internalHost.ExitNestedPrompt();

        public override void NotifyBeginApplication() => _internalHost.NotifyBeginApplication();

        public override void NotifyEndApplication() => _internalHost.NotifyEndApplication();

        public void PopRunspace() => _internalHost.PopRunspace();

        public void PushRunspace(System.Management.Automation.Runspaces.Runspace runspace) => _internalHost.PushRunspace(runspace);

        public override void SetShouldExit(int exitCode) => _internalHost.SetShouldExit(exitCode);
    }
}
