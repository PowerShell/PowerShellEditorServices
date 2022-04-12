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
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;
        private readonly IDebugAdapterServerFacade _debugAdapterServer;

        private readonly IPowerShellDebugContext _debugContext;

        public DebugEventHandlerService(
            ILoggerFactory factory,
            IInternalPowerShellExecutionService executionService,
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
            _executionService.RunspaceChanged += OnRunspaceChanged;
            _debugService.BreakpointUpdated += OnBreakpointUpdated;
            _debugService.DebuggerStopped += OnDebuggerStopped;
            _debugContext.DebuggerResuming += OnDebuggerResuming;
        }

        internal void UnregisterEventHandlers()
        {
            _executionService.RunspaceChanged -= OnRunspaceChanged;
            _debugService.BreakpointUpdated -= OnBreakpointUpdated;
            _debugService.DebuggerStopped -= OnDebuggerStopped;
            _debugContext.DebuggerResuming -= OnDebuggerResuming;
        }

        #region Public methods

        internal void TriggerDebuggerStopped(DebuggerStoppedEventArgs e) => OnDebuggerStopped(null, e);

        #endregion

        #region Event Handlers

        private void OnDebuggerStopped(object sender, DebuggerStoppedEventArgs e)
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

        private void OnRunspaceChanged(object sender, RunspaceChangedEventArgs e)
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
                                AllThreadsContinued = true,
                            });
                    }
                    return;
            }
        }

        private void OnDebuggerResuming(object sender, DebuggerResumingEventArgs e)
        {
            _debugAdapterServer.SendNotification(EventNames.Continued,
                new ContinuedEvent
                {
                    ThreadId = ThreadsHandler.PipelineThread.Id,
                    AllThreadsContinued = true,
                });
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            // Don't send breakpoint update notifications when setting
            // breakpoints on behalf of the client.
            if (_debugStateService.IsSetBreakpointInProgress)
            {
                return;
            }

            if (e.Breakpoint is LineBreakpoint)
            {
                OmniSharp.Extensions.DebugAdapter.Protocol.Models.Breakpoint breakpoint = LspDebugUtils.CreateBreakpoint(
                    BreakpointDetails.Create(e.Breakpoint, e.UpdateType)
                );

                string reason = e.UpdateType switch
                {
                    BreakpointUpdateType.Set => BreakpointEventReason.New,
                    BreakpointUpdateType.Removed => BreakpointEventReason.Removed,
                    BreakpointUpdateType.Enabled => BreakpointEventReason.Changed,
                    BreakpointUpdateType.Disabled => BreakpointEventReason.Changed,
                    _ => "InvalidBreakpointUpdateTypeEnum"
                };

                _debugAdapterServer.SendNotification(
                    EventNames.Breakpoint,
                    new BreakpointEvent { Breakpoint = breakpoint, Reason = reason }
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
