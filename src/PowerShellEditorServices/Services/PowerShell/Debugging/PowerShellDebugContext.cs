using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using System;
using System.Management.Automation;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
    using OmniSharp.Extensions.LanguageServer.Protocol.Server;

    internal class PowerShellDebugContext : IPowerShellDebugContext
    {
        private readonly ILanguageServer _languageServer;

        private readonly PowerShellContext _pwshContext;

        private readonly ConsoleReplRunner _consoleRepl;

        public PowerShellDebugContext(
            ILanguageServer languageServer,
            PowerShellContext pwshContext,
            ConsoleReplRunner consoleReplRunner)
        {
            _languageServer = languageServer;
            _pwshContext = pwshContext;
            _consoleRepl = consoleReplRunner;
        }

        private CancellationTokenSource _debugLoopCancellationSource;
        
        public bool IsStopped { get; private set; }

        public DscBreakpointCapability DscBreakpointCapability => throw new NotImplementedException();

        public DebuggerStopEventArgs LastStopEventArgs { get; set; }

        public CancellationToken OnResumeCancellationToken => _debugLoopCancellationSource.Token;

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;
        public event Action<object, DebuggerResumingEventArgs> DebuggerResuming;
        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        public void Abort()
        {
            SetDebugResuming(DebuggerResumeAction.Stop);
        }

        public void BreakExecution()
        {
            _pwshContext.CurrentRunspace.Debugger.SetDebuggerStepMode(enabled: true);
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

        private void RaiseDebuggerStoppedEvent()
        {
            // TODO: Send language server message to start debugger
            DebuggerStopped?.Invoke(this, LastStopEventArgs);
        }

        public void RaiseDebuggerResumingEvent(DebuggerResumingEventArgs debuggerResumingEventArgs)
        {
            DebuggerResuming?.Invoke(this, debuggerResumingEventArgs);
        }

        public void HandleBreakpointUpdated(BreakpointUpdatedEventArgs breakpointUpdatedEventArgs)
        {
            BreakpointUpdated?.Invoke(this, breakpointUpdatedEventArgs);
        }
    }
}
