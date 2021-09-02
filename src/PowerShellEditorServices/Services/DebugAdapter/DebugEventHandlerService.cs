// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class DebugEventHandlerService
    {
        private readonly ILogger<DebugEventHandlerService> _logger;
        private readonly PowerShellExecutionService _executionService;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;
        private readonly IDebugAdapterServerFacade _debugAdapterServer;

        private readonly IPowerShellDebugContext _debugContext;

        public DebugEventHandlerService(
            ILoggerFactory factory,
            PowerShellExecutionService executionService,
            DebugService debugService,
            DebugStateService debugStateService,
            IDebugAdapterServerFacade debugAdapterServer,
            IPowerShellDebugContext debugContext)
        {
            _logger = factory.CreateLogger<DebugEventHandlerService>();
            _executionService = executionService;
            _debugService = debugService;
            _debugStateService = debugStateService;
            _debugAdapterServer = debugAdapterServer;
            _debugContext = debugContext;
        }

        internal void RegisterEventHandlers()
        {
            _executionService.RunspaceChanged += ExecutionService_RunspaceChanged;
            _debugService.BreakpointUpdated += DebugService_BreakpointUpdated;
            _debugService.DebuggerStopped += DebugService_DebuggerStopped;
            _debugContext.DebuggerResuming += PowerShellContext_DebuggerResuming;
        }

        internal void UnregisterEventHandlers()
        {
            _executionService.RunspaceChanged -= ExecutionService_RunspaceChanged;
            _debugService.BreakpointUpdated -= DebugService_BreakpointUpdated;
            _debugService.DebuggerStopped -= DebugService_DebuggerStopped;
            _debugContext.DebuggerResuming -= PowerShellContext_DebuggerResuming;
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
                    Reason = debuggerStoppedReason
                });
        }

        private void ExecutionService_RunspaceChanged(object sender, RunspaceChangedEventArgs e)
        {
            switch (e.ChangeAction)
            {
                case RunspaceChangeAction.Enter:
                    if (_debugStateService.WaitingForAttach
                        && e.NewRunspace.RunspaceOrigin == RunspaceOrigin.DebuggedRunspace)
                    {
                        // Sends the InitializedEvent so that the debugger will continue
                        // sending configuration requests
                        _debugStateService.WaitingForAttach = false;
                        _debugStateService.ServerStarted.SetResult(true);
                    }
                    return;

                case RunspaceChangeAction.Exit:
                    if (_debugContext.IsStopped)
                    {
                        // Exited the session while the debugger is stopped,
                        // send a ContinuedEvent so that the client changes the
                        // UI to appear to be running again
                        _debugAdapterServer.SendNotification(
                            EventNames.Continued,
                            new ContinuedEvent
                            {
                                ThreadId = ThreadsHandler.PipelineThread.Id,
                            });
                    }
                    return;
            }
        }

        private void PowerShellContext_DebuggerResuming(object sender, DebuggerResumingEventArgs e)
        {
            _debugAdapterServer.SendNotification(EventNames.Continued,
                new ContinuedEvent
                {
                    AllThreadsContinued = true,
                    ThreadId = ThreadsHandler.PipelineThread.Id,
                });
        }

        private void DebugService_BreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            string reason = "changed";

            if (_debugStateService.IsSetBreakpointInProgress)
            {
                // Don't send breakpoint update notifications when setting
                // breakpoints on behalf of the client.
                return;
            }

            switch (e.UpdateType)
            {
                case BreakpointUpdateType.Set:
                    reason = "new";
                    break;

                case BreakpointUpdateType.Removed:
                    reason = "removed";
                    break;
            }

            var breakpoint = new OmniSharp.Extensions.DebugAdapter.Protocol.Models.Breakpoint
            {
                Verified = e.UpdateType != BreakpointUpdateType.Disabled
            };

            if (e.Breakpoint is LineBreakpoint)
            {
                breakpoint = LspDebugUtils.CreateBreakpoint(BreakpointDetails.Create(e.Breakpoint));
            }
            else if (e.Breakpoint is CommandBreakpoint)
            {
                _logger.LogTrace("Function breakpoint updated event is not supported yet");
                return;
            }
            else
            {
                _logger.LogError($"Unrecognized breakpoint type {e.Breakpoint.GetType().FullName}");
                return;
            }

            _debugAdapterServer.SendNotification(EventNames.Breakpoint,
                new BreakpointEvent
                {
                    Reason = reason,
                    Breakpoint = breakpoint
                });
        }

        #endregion
    }
}
