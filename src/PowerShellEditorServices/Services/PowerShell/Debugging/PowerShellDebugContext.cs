using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using System;
using System.Management.Automation;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    using Microsoft.Extensions.Logging;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
    using OmniSharp.Extensions.LanguageServer.Protocol.Server;
    using System.Threading.Tasks;

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

        private readonly EditorServicesConsolePSHost _psesHost;

        private readonly ConsoleReplRunner _consoleRepl;

        public PowerShellDebugContext(
            ILoggerFactory loggerFactory,
            ILanguageServerFacade languageServer,
            EditorServicesConsolePSHost psesHost,
            ConsoleReplRunner consoleReplRunner)
        {
            _logger = loggerFactory.CreateLogger<PowerShellDebugContext>();
            _languageServer = languageServer;
            _psesHost = psesHost;
            _consoleRepl = consoleReplRunner;
        }

        private CancellationTokenSource _debugLoopCancellationSource;

        public bool IsStopped { get; private set; }

        public DscBreakpointCapability DscBreakpointCapability => throw new NotImplementedException();

        public DebuggerStopEventArgs LastStopEventArgs { get; private set; }

        public CancellationToken OnResumeCancellationToken => _debugLoopCancellationSource.Token;

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;
        public event Action<object, DebuggerResumingEventArgs> DebuggerResuming;
        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        public Task<DscBreakpointCapability> GetDscBreakpointCapabilityAsync(CancellationToken cancellationToken)
        {
            return _psesHost.CurrentRunspace.GetDscBreakpointCapabilityAsync(_logger, _psesHost.ExecutionService, cancellationToken);
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
            _consoleRepl?.SetReplPop();
            LastStopEventArgs.ResumeAction = debuggerResumeAction;
            _debugLoopCancellationSource.Cancel();
        }

        public void EnterDebugLoop(CancellationToken loopCancellationToken)
        {
            _debugLoopCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(loopCancellationToken);
            RaiseDebuggerStoppedEvent();

        }

        public void ExitDebugLoop()
        {
            _debugLoopCancellationSource.Dispose();
            _debugLoopCancellationSource = null;
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
            if (debuggerResult.ResumeAction != null)
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
            // TODO: Send language server message to start debugger
            DebuggerStopped?.Invoke(this, LastStopEventArgs);
        }

        private void RaiseDebuggerResumingEvent(DebuggerResumingEventArgs debuggerResumingEventArgs)
        {
            DebuggerResuming?.Invoke(this, debuggerResumingEventArgs);
        }
    }
}
