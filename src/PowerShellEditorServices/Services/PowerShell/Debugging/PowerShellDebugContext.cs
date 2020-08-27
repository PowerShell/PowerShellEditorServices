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

    internal class PowerShellDebugContext : IPowerShellDebugContext
    {
        private readonly ILogger _logger;

        private readonly ILanguageServer _languageServer;

        private readonly EditorServicesConsolePSHost _psesHost;

        private readonly ConsoleReplRunner _consoleRepl;

        public PowerShellDebugContext(
            ILoggerFactory loggerFactory,
            ILanguageServer languageServer,
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
