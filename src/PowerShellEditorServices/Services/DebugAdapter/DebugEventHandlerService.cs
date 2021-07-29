// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class DebugEventHandlerService
    {
        private readonly ILogger<DebugEventHandlerService> _logger;
        private readonly PowerShellContextService _powerShellContextService;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;
        private readonly IDebugAdapterServerFacade _debugAdapterServer;

        public DebugEventHandlerService(
            ILoggerFactory factory,
            PowerShellContextService powerShellContextService,
            DebugService debugService,
            DebugStateService debugStateService,
            IDebugAdapterServerFacade debugAdapterServer)
        {
            _logger = factory.CreateLogger<DebugEventHandlerService>();
            _powerShellContextService = powerShellContextService;
            _debugService = debugService;
            _debugStateService = debugStateService;
            _debugAdapterServer = debugAdapterServer;
        }

        internal void RegisterEventHandlers()
        {
            _powerShellContextService.RunspaceChanged += PowerShellContext_RunspaceChanged;
            _debugService.BreakpointUpdated += DebugService_BreakpointUpdated;
            _debugService.DebuggerStopped += DebugService_DebuggerStopped;
            _powerShellContextService.DebuggerResumed += PowerShellContext_DebuggerResumed;
        }

        internal void UnregisterEventHandlers()
        {
            _powerShellContextService.RunspaceChanged -= PowerShellContext_RunspaceChanged;
            _debugService.BreakpointUpdated -= DebugService_BreakpointUpdated;
            _debugService.DebuggerStopped -= DebugService_DebuggerStopped;
            _powerShellContextService.DebuggerResumed -= PowerShellContext_DebuggerResumed;
        }

        #region Public methods

        internal void TriggerDebuggerStopped(DebuggerStoppedEventArgs e)
        {
            DebugService_DebuggerStopped(null, e);
        }

        #endregion

        #region Event Handlers

        private void DebugService_DebuggerStopped(object sender, DebuggerStoppedEventArgs e)
        {
            // Provide the reason for why the debugger has stopped script execution.
            // See https://github.com/Microsoft/vscode/issues/3648
            // The reason is displayed in the breakpoints viewlet.  Some recommended reasons are:
            // "step", "breakpoint", "function breakpoint", "exception" and "pause".
            // We don't support exception breakpoints and for "pause", we can't distinguish
            // between stepping and the user pressing the pause/break button in the debug toolbar.
            string debuggerStoppedReason = "step";
            if (e.OriginalEvent.Breakpoints.Count > 0)
            {
                debuggerStoppedReason =
                    e.OriginalEvent.Breakpoints[0] is CommandBreakpoint
                        ? "function breakpoint"
                        : "breakpoint";
            }

            _debugAdapterServer.SendNotification(EventNames.Stopped,
                new StoppedEvent
                {
                    ThreadId = 1,
                    AllThreadsStopped = true,
                    Reason = debuggerStoppedReason
                });
        }

        private void PowerShellContext_RunspaceChanged(object sender, RunspaceChangedEventArgs e)
        {
            if (_debugStateService.WaitingForAttach &&
                e.ChangeAction == RunspaceChangeAction.Enter &&
                e.NewRunspace.Context == RunspaceContext.DebuggedRunspace)
            {
                // Sends the InitializedEvent so that the debugger will continue
                // sending configuration requests
                _debugStateService.WaitingForAttach = false;
                _debugStateService.ServerStarted.SetResult(true);
            }
            else if (
                e.ChangeAction == RunspaceChangeAction.Exit &&
                _powerShellContextService.IsDebuggerStopped)
            {
                // Exited the session while the debugger is stopped,
                // send a ContinuedEvent so that the client changes the
                // UI to appear to be running again
                _debugAdapterServer.SendNotification(EventNames.Continued,
                    new ContinuedEvent
                    {
                        ThreadId = 1,
                        AllThreadsContinued = true
                    });
            }
        }

        private void PowerShellContext_DebuggerResumed(object sender, DebuggerResumeAction e)
        {
            _debugAdapterServer.SendNotification(EventNames.Continued,
                new ContinuedEvent
                {
                    ThreadId = 1,
                    AllThreadsContinued = true
                });
        }

        private void DebugService_BreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            // Don't send breakpoint update notifications when setting
            // breakpoints on behalf of the client.
            if (_debugStateService.IsSetBreakpointInProgress)
            {
                return;
            }

            if (e.Breakpoint is LineBreakpoint)
            {
                var breakpoint = LspDebugUtils.CreateBreakpoint(
                    BreakpointDetails.Create(e.Breakpoint, e.UpdateType)
                );

                string reason = (e.UpdateType) switch {
                    BreakpointUpdateType.Set => "new",
                    BreakpointUpdateType.Removed => "removed",
                    BreakpointUpdateType.Enabled => "changed",
                    BreakpointUpdateType.Disabled => "changed",
                    _ => "unknown"
                };

                _debugAdapterServer.SendNotification(
                    EventNames.Breakpoint,
                    new BreakpointEvent { Reason = reason, Breakpoint = breakpoint }
                );
            }
            else if (e.Breakpoint is CommandBreakpoint)
            {
                _logger.LogTrace("Function breakpoint updated event is not supported yet");
            }
            else
            {
                _logger.LogError($"Unrecognized breakpoint type {e.Breakpoint.GetType().FullName}");
            }
        }

        #endregion
    }
}
