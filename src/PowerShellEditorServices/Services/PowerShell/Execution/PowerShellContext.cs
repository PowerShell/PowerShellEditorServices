using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    [Flags]
    internal enum PromptFrameType
    {
        Normal = 0,
        NestedPrompt = 1,
        Debug = 2,
        Remote = 4,
    }

    internal class PowerShellPushedArgs
    {
        public PowerShellPushedArgs(PromptFrameType frameType)
        {
            FrameType = frameType;
        }

        public PromptFrameType FrameType { get; }
    }

    internal class PowerShellPoppedArgs
    {
        public PowerShellPoppedArgs(PromptFrameType frameType)
        {
            FrameType = frameType;
        }

        public PromptFrameType FrameType { get; }
    }

    internal class DebuggerResumingArgs
    {
        public DebuggerResumingArgs(DebuggerResumeAction? resumeAction)
        {
            ResumeAction = resumeAction;
        }

        public DebuggerResumeAction? ResumeAction { get; }
    }

    internal class PromptCancellationRequestedArgs
    {
    }

    internal class NestedPromptExitingArgs
    {
    }

    internal class PowerShellContext : IDisposable
    {
        public static PowerShellContext Create(EditorServicesConsolePSHost psHost, PSLanguageMode languageMode)
        {
            InitialSessionState iss = Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1"
                ? InitialSessionState.CreateDefault()
                : InitialSessionState.CreateDefault2();

            iss.LanguageMode = languageMode;

            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, iss);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;

            runspace.Open();

            var context = new PowerShellContext();
            context.PushInitialPowerShell(runspace);

            Runspace.DefaultRunspace = runspace;

            return context;
        }

        private readonly Stack<ContextFrame> _frameStack;

        private PowerShellContext()
        {
            _frameStack = new Stack<ContextFrame>();
        }

        public int PowerShellDepth => _frameStack.Count;

        public SMA.PowerShell CurrentPowerShell
        {
            get => _frameStack.Peek().PowerShell;
        }

        public CancellationTokenSource CurrentCancellationSource
        {
            get => _frameStack.Peek().CancellationTokenSource;
        }

        public bool IsExiting { get; private set; }

        public event Action<object, PowerShellPushedArgs> PowerShellPushed;

        public event Action<object, PowerShellPoppedArgs> PowerShellPopped;

        public event Action<object, NestedPromptExitingArgs> NestedPromptExiting;

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;

        public event Action<object, DebuggerResumingArgs> DebuggerResumed;

        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        public void BeginExiting()
        {
            IsExiting = true;
            NestedPromptExiting?.Invoke(this, new NestedPromptExitingArgs());
        }

        public void ProcessDebuggerResult(DebuggerCommandResults result)
        {
            if (result.ResumeAction != null)
            {
                DebuggerResumed?.Invoke(this, new DebuggerResumingArgs(result.ResumeAction));
            }
        }

        public void PushNestedPowerShell()
        {
            PushNestedPowerShell(PromptFrameType.Normal);
        }

        public void PushDebugPowerShell()
        {
            PushNestedPowerShell(PromptFrameType.Debug);
        }

        public void PushPowerShell(Runspace runspace)
        {
            var pwsh = SMA.PowerShell.Create();
            pwsh.Runspace = runspace;

            PromptFrameType frameType = PromptFrameType.Normal;

            if (runspace.RunspaceIsRemote)
            {
                frameType |= PromptFrameType.Remote;
            }

            PushFrame(new ContextFrame(pwsh, frameType, new CancellationTokenSource()));
        }

        public void PopPowerShell()
        {
            PopFrame();
        }

        public bool TryPopPowerShell()
        {
            if (_frameStack.Count <= 1)
            {
                return false;
            }

            PopFrame();
            return true;
        }

        public void Dispose()
        {
            while (_frameStack.Count > 0)
            {
                _frameStack.Pop().PowerShell.Dispose();
            }
        }

        private void PushInitialPowerShell(Runspace runspace)
        {
            var pwsh = SMA.PowerShell.Create();
            pwsh.Runspace = runspace;

            PushFrame(new ContextFrame(pwsh, PromptFrameType.Normal, new CancellationTokenSource()));
        }

        private void PushNestedPowerShell(PromptFrameType frameType)
        {
            SMA.PowerShell pwsh = CreateNestedPowerShell();
            PromptFrameType newFrameType = _frameStack.Peek().FrameType | PromptFrameType.NestedPrompt | frameType;
            PushFrame(new ContextFrame(pwsh, newFrameType, new CancellationTokenSource()));
        }

        private SMA.PowerShell CreateNestedPowerShell()
        {
            // PowerShell.CreateNestedPowerShell() sets IsNested but not IsChild
            // So we must use the RunspaceMode.CurrentRunspace option on PowerShell.Create() instead
            var pwsh = SMA.PowerShell.Create(RunspaceMode.CurrentRunspace);
            pwsh.Runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            return pwsh;
        }

        private void PushFrame(ContextFrame frame)
        {
            _frameStack.Push(frame);
            frame.PowerShell.Runspace.Debugger.DebuggerStop += OnDebuggerStopped;
            frame.PowerShell.Runspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
            PowerShellPushed?.Invoke(this, new PowerShellPushedArgs(frame.FrameType));
        }

        private void PopFrame()
        {
            IsExiting = false;
            ContextFrame frame = _frameStack.Pop();
            try
            {
                frame.PowerShell.Runspace.Debugger.DebuggerStop -= OnDebuggerStopped;
                frame.PowerShell.Runspace.Debugger.BreakpointUpdated -= OnBreakpointUpdated;
                PowerShellPopped?.Invoke(this, new PowerShellPoppedArgs(frame.FrameType));
            }
            finally
            {
                frame.Dispose();
            }
        }

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs args)
        {
            DebuggerStopped?.Invoke(this, args);
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs args)
        {
            BreakpointUpdated?.Invoke(this, args);
        }

        private class ContextFrame : IDisposable
        {
            private bool disposedValue;

            public ContextFrame(SMA.PowerShell powerShell, PromptFrameType frameType, CancellationTokenSource cancellationTokenSource)
            {
                PowerShell = powerShell;
                FrameType = frameType;
                CancellationTokenSource = cancellationTokenSource;
            }

            public SMA.PowerShell PowerShell { get; }

            public PromptFrameType FrameType { get; }

            public CancellationTokenSource CancellationTokenSource { get; }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        PowerShell.Dispose();
                        CancellationTokenSource.Dispose();
                    }

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}
