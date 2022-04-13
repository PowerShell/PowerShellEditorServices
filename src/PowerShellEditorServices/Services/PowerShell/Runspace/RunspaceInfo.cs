// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    internal class RunspaceInfo : IRunspaceInfo
    {
        public static RunspaceInfo CreateFromLocalPowerShell(
            ILogger logger,
            PowerShell pwsh)
        {
            PowerShellVersionDetails psVersionDetails = PowerShellVersionDetails.GetVersionDetails(logger, pwsh);
            SessionDetails sessionDetails = SessionDetails.GetFromPowerShell(pwsh);

            return new RunspaceInfo(
                pwsh.Runspace,
                RunspaceOrigin.Local,
                psVersionDetails,
                sessionDetails,
                isRemote: false);
        }

        public static RunspaceInfo CreateFromPowerShell(
            ILogger logger,
            PowerShell pwsh,
            string localComputerName)
        {
            PowerShellVersionDetails psVersionDetails = PowerShellVersionDetails.GetVersionDetails(logger, pwsh);
            SessionDetails sessionDetails = SessionDetails.GetFromPowerShell(pwsh);

            bool isOnLocalMachine = string.Equals(sessionDetails.ComputerName, localComputerName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sessionDetails.ComputerName, "localhost", StringComparison.OrdinalIgnoreCase);

            RunspaceOrigin runspaceOrigin = RunspaceOrigin.Local;
            if (pwsh.Runspace.RunspaceIsRemote)
            {
                runspaceOrigin = pwsh.Runspace.ConnectionInfo is NamedPipeConnectionInfo
                    ? RunspaceOrigin.EnteredProcess
                    : RunspaceOrigin.PSSession;
            }

            return new RunspaceInfo(
                pwsh.Runspace,
                runspaceOrigin,
                psVersionDetails,
                sessionDetails,
                isRemote: !isOnLocalMachine);
        }

        private DscBreakpointCapability _dscBreakpointCapability;

        public RunspaceInfo(
            Runspace runspace,
            RunspaceOrigin origin,
            PowerShellVersionDetails powerShellVersionDetails,
            SessionDetails sessionDetails,
            bool isRemote)
        {
            Runspace = runspace;
            RunspaceOrigin = origin;
            SessionDetails = sessionDetails;
            PowerShellVersionDetails = powerShellVersionDetails;
            IsOnRemoteMachine = isRemote;
        }

        public RunspaceOrigin RunspaceOrigin { get; }

        public PowerShellVersionDetails PowerShellVersionDetails { get; }

        public SessionDetails SessionDetails { get; }

        public Runspace Runspace { get; }

        public bool IsOnRemoteMachine { get; }

        public async Task<DscBreakpointCapability> GetDscBreakpointCapabilityAsync(
            ILogger logger,
            PsesInternalHost psesHost,
            CancellationToken cancellationToken)
        {
            return _dscBreakpointCapability ??= await DscBreakpointCapability.GetDscCapabilityAsync(
                    logger,
                    this,
                    psesHost,
                    cancellationToken)
                    .ConfigureAwait(false);
        }
    }
}
