// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    /// <summary>
    /// Handles the state of the PowerShell debugger.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Debugging through a PowerShell Host is implemented by registering a handler for the <see
    /// cref="Debugger.DebuggerStop"/> event. Registering that handler causes debug actions in
    /// PowerShell like Set-PSBreakpoint and Wait-Debugger to drop into the debugger and trigger the
    /// handler. The handler is passed a mutable <see cref="DebuggerStopEventArgs"/> object and the
    /// debugger stop lasts for the duration of the handler call. The handler sets the <see
    /// cref="DebuggerStopEventArgs.ResumeAction"/> property when after it returns, the PowerShell
    /// debugger uses that as the direction on how to proceed.
    /// </para>
    /// <para>
    /// When we handle the <see cref="Debugger.DebuggerStop"/> event, we drop into a nested debug
    /// prompt and execute things in the debugger with <see cref="Debugger.ProcessCommand(PSCommand,
    /// PSDataCollection{PSObject})"/>, which enables debugger commands like <c>l</c>, <c>c</c>,
    /// <c>s</c>, etc. <see cref="PowerShellDebugContext"/> saves the event args object in its
    /// state, and when one of the debugger commands is used, the result returned is used to set
    /// <see cref="DebuggerStopEventArgs.ResumeAction"/> on the saved event args object so that when
    /// the event handler returns, the PowerShell debugger takes the correct action.
    /// </para>
    /// </remarks>
    internal class PowerShellDebugContext : IPowerShellDebugContext
    {
        private readonly ILogger _logger;

        private readonly PsesInternalHost _psesHost;

        public PowerShellDebugContext(
            ILoggerFactory loggerFactory,
            PsesInternalHost psesHost)
        {
            _logger = loggerFactory.CreateLogger<PowerShellDebugContext>();
            _psesHost = psesHost;
        }

        /// <summary>
        /// Tracks if the debugger is currently stopped at a breakpoint.
        /// </summary>
        public bool IsStopped { get; private set; }

        /// <summary>
        /// Tracks the state of the PowerShell debugger. This is NOT the same as <see
        /// cref="Debugger.IsActive">, which is true whenever breakpoints are set. Instead, this is
        /// set to true when the first <see cref="PsesInternalHost.OnDebuggerStopped"> event is
        /// fired, and set to false in <see cref="PsesInternalHost.DoOneRepl"> when <see
        /// cref="Debugger.IsInBreakpoint"> is false. This is used to send the
        /// 'powershell/stopDebugger' notification to the LSP debug server in the cases where the
        /// server was started or ended by the PowerShell session instead of by Code's GUI.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Tracks the state of the LSP debug server (not the PowerShell debugger).
        /// </summary>
        public bool IsDebugServerActive { get; set; }

        /// <summary>
        /// Tracks whether we are running <c>Debug-Runspace</c> in an out-of-process runspace.
        /// </summary>
        public bool IsDebuggingRemoteRunspace { get; set; }

        public DebuggerStopEventArgs LastStopEventArgs { get; private set; }

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;
        public event Action<object, DebuggerResumingEventArgs> DebuggerResuming;
        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        public Task<DscBreakpointCapability> GetDscBreakpointCapabilityAsync(CancellationToken cancellationToken)
        {
            _psesHost.Runspace.ThrowCancelledIfUnusable();
            return _psesHost.CurrentRunspace.GetDscBreakpointCapabilityAsync(_logger, _psesHost, cancellationToken);
        }

        // This is required by the PowerShell API so that remote debugging works. Without it, a
        // runspace may not have these options set and attempting to set breakpoints remotely fails.
        public void EnableDebugMode()
        {
            _psesHost.Runspace.ThrowCancelledIfUnusable();
            _psesHost.Runspace.Debugger.SetDebugMode(DebugModes.LocalScript | DebugModes.RemoteScript);
        }

        public void Abort() => SetDebugResuming(DebuggerResumeAction.Stop, isDisconnect: true);

        public void BreakExecution() => _psesHost.Runspace.Debugger.SetDebuggerStepMode(enabled: true);

        public void Continue() => SetDebugResuming(DebuggerResumeAction.Continue);

        public void StepInto() => SetDebugResuming(DebuggerResumeAction.StepInto);

        public void StepOut() => SetDebugResuming(DebuggerResumeAction.StepOut);

        public void StepOver() => SetDebugResuming(DebuggerResumeAction.StepOver);

        public void SetDebugResuming(DebuggerResumeAction debuggerResumeAction, bool isDisconnect = false)
        {
            // NOTE: We exit because the paused/stopped debugger is currently in a prompt REPL, and
            // to resume the debugger we must exit that REPL.
            _psesHost.SetExit();

            if (LastStopEventArgs is not null)
            {
                LastStopEventArgs.ResumeAction = debuggerResumeAction;
            }

            // We need to tell whatever is happening right now in the debug prompt to wrap up so we
            // can continue. However, if the host was initialized with the console REPL disabled,
            // then we'd accidentally cancel the debugged task since no prompt is running. We can
            // test this by checking if the UI's type is NullPSHostUI which is used specifically in
            // this scenario. This mostly applies to unit tests.
            if (_psesHost.UI is NullPSHostUI)
            {
                return;
            }

            if (debuggerResumeAction is DebuggerResumeAction.Stop)
            {
                if (isDisconnect)
                {
                    _psesHost.UnwindCallStack();
                    return;
                }

                _psesHost.CancelIdleParentTask();
                return;
            }

            if ((_psesHost.CurrentFrame.FrameType & PowerShellFrameType.Repl) is not 0)
            {
                _psesHost.CancelCurrentTask();
            }
        }

        // This must be called AFTER the new PowerShell has been pushed
        public void EnterDebugLoop() => RaiseDebuggerStoppedEvent();

        // This must be called BEFORE the debug PowerShell has been popped
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "This method may acquire an implementation later, at which point it will need instance data")]
        public void ExitDebugLoop() { }

        public void SetDebuggerStopped(DebuggerStopEventArgs args)
        {
            IsStopped = true;
            LastStopEventArgs = args;
        }

        public void SetDebuggerResumed() => IsStopped = false;

        public void ProcessDebuggerResult(DebuggerCommandResults debuggerResult)
        {
            if (debuggerResult?.ResumeAction is not null)
            {
                SetDebugResuming(debuggerResult.ResumeAction.Value);

                // If a debugging command like `c` is specified in a nested remote
                // debugging prompt we need to unwind the nested execution loop.
                if ((_psesHost.CurrentFrame.FrameType & PowerShellFrameType.Remote) is not 0)
                {
                    _psesHost.ForceSetExit();
                }

                RaiseDebuggerResumingEvent(new DebuggerResumingEventArgs(debuggerResult.ResumeAction.Value));

                // The Terminate exception is used by the engine for flow control
                // when it needs to unwind the callstack out of the debugger.
                if (debuggerResult.ResumeAction is DebuggerResumeAction.Stop)
                {
                    throw new TerminateException();
                }
            }
        }

        public void HandleBreakpointUpdated(BreakpointUpdatedEventArgs args) => BreakpointUpdated?.Invoke(this, args);

        private void RaiseDebuggerStoppedEvent() => DebuggerStopped?.Invoke(this, LastStopEventArgs);

        private void RaiseDebuggerResumingEvent(DebuggerResumingEventArgs args) => DebuggerResuming?.Invoke(this, args);
    }
}
