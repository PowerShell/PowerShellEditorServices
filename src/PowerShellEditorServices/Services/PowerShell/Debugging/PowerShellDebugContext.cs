// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    /// <summary>
    /// Handles the state of the PowerShell debugger.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Debugging through a PowerShell Host is implemented by registering a handler
    /// for the <see cref="System.Management.Automation.Debugger.DebuggerStop"/> event.
    /// Registering that handler causes debug actions in PowerShell like Set-PSBreakpoint
    /// and Wait-Debugger to drop into the debugger and trigger the handler.
    /// The handler is passed a mutable <see cref="System.Management.Automation.DebuggerStopEventArgs"/> object
    /// and the debugger stop lasts for the duration of the handler call.
    /// The handler sets the <see cref="System.Management.Automation.DebuggerStopEventArgs.ResumeAction"/> property
    /// when after it returns, the PowerShell debugger uses that as the direction on how to proceed.
    /// </para>
    /// <para>
    /// When we handle the <see cref="System.Management.Automation.Debugger.DebuggerStop"/> event,
    /// we drop into a nested debug prompt and execute things in the debugger with <see cref="System.Management.Automation.Debugger.ProcessCommand(PSCommand, PSDataCollection{PSObject})"/>,
    /// which enables debugger commands like <c>l</c>, <c>c</c>, <c>s</c>, etc.
    /// <see cref="Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging.PowerShellDebugContext"/> saves the event args object in its state,
    /// and when one of the debugger commands is used, the result returned is used to set <see cref="System.Management.Automation.DebuggerStopEventArgs.ResumeAction"/>
    /// on the saved event args object so that when the event handler returns, the PowerShell debugger takes the correct action.
    /// </para>
    /// </remarks>
    internal class PowerShellDebugContext : IPowerShellDebugContext
    {
        private readonly ILogger _logger;

        private readonly ILanguageServerFacade _languageServer;

        private readonly PsesInternalHost _psesHost;

        public PowerShellDebugContext(
            ILoggerFactory loggerFactory,
            ILanguageServerFacade languageServer,
            PsesInternalHost psesHost)
        {
            _logger = loggerFactory.CreateLogger<PowerShellDebugContext>();
            _languageServer = languageServer;
            _psesHost = psesHost;
        }

        public bool IsStopped { get; private set; }

        public bool IsDebugServerActive { get; set; }

        public DebuggerStopEventArgs LastStopEventArgs { get; private set; }

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;
        public event Action<object, DebuggerResumingEventArgs> DebuggerResuming;
        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        public Task<DscBreakpointCapability> GetDscBreakpointCapabilityAsync(CancellationToken cancellationToken)
        {
            return _psesHost.CurrentRunspace.GetDscBreakpointCapabilityAsync(_logger, _psesHost, cancellationToken);
        }

        public void EnableDebugMode()
        {
            // This is required by the PowerShell API so that remote debugging works.
            // Without it, a runspace may not have these options set and attempting to set breakpoints remotely can fail.
            _psesHost.Runspace.Debugger.SetDebugMode(DebugModes.LocalScript | DebugModes.RemoteScript);
        }

        public void Abort()
        {
            SetDebugResuming(DebuggerResumeAction.Stop);
        }

        public void BreakExecution()
        {
            _psesHost.Runspace.Debugger.SetDebuggerStepMode(enabled: true);
        }

        public void Continue()
        {
            SetDebugResuming(DebuggerResumeAction.Continue);
        }

        public void StepInto()
        {
            SetDebugResuming(DebuggerResumeAction.StepInto);
        }

        public void StepOut()
        {
            SetDebugResuming(DebuggerResumeAction.StepOut);
        }

        public void StepOver()
        {
            SetDebugResuming(DebuggerResumeAction.StepOver);
        }

        public void SetDebugResuming(DebuggerResumeAction debuggerResumeAction)
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
            if (_psesHost.UI is not NullPSHostUI)
            {
                _psesHost.CancelCurrentTask();
            }
        }

        // This must be called AFTER the new PowerShell has been pushed
        public void EnterDebugLoop()
        {
            RaiseDebuggerStoppedEvent();
        }

        // This must be called BEFORE the debug PowerShell has been popped
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "This method may acquire an implementation later, at which point it will need instance data")]
        public void ExitDebugLoop()
        {
        }

        public void SetDebuggerStopped(DebuggerStopEventArgs debuggerStopEventArgs)
        {
            IsStopped = true;
            LastStopEventArgs = debuggerStopEventArgs;
        }

        public void SetDebuggerResumed()
        {
            IsStopped = false;
        }

        public void ProcessDebuggerResult(DebuggerCommandResults debuggerResult)
        {
            if (debuggerResult?.ResumeAction is not null)
            {
                SetDebugResuming(debuggerResult.ResumeAction.Value);
                RaiseDebuggerResumingEvent(new DebuggerResumingEventArgs(debuggerResult.ResumeAction.Value));
            }
        }

        public void HandleBreakpointUpdated(BreakpointUpdatedEventArgs breakpointUpdatedEventArgs)
        {
            BreakpointUpdated?.Invoke(this, breakpointUpdatedEventArgs);
        }

        private void RaiseDebuggerStoppedEvent()
        {
            if (!IsDebugServerActive)
            {
                // NOTE: The language server is not necessarily connected, so this must be
                // conditional access. This shows up in unit tests.
                _languageServer?.SendNotification("powerShell/startDebugger");
            }

            DebuggerStopped?.Invoke(this, LastStopEventArgs);
        }

        private void RaiseDebuggerResumingEvent(DebuggerResumingEventArgs debuggerResumingEventArgs)
        {
            DebuggerResuming?.Invoke(this, debuggerResumingEventArgs);
        }
    }
}
