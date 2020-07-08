using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
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

    internal class PromptFramePushedArgs
    {
        public PromptFramePushedArgs(PromptFrameType frameType)
        {
            FrameType = frameType;
        }

        public PromptFrameType FrameType { get; }
    }

    internal class PromptFramePoppedArgs
    {
        public PromptFramePoppedArgs(PromptFrameType frameType)
        {
            FrameType = frameType;
        }

        public PromptFrameType FrameType { get; }
    }

    internal class DebuggerResumedArgs
    {
        public DebuggerResumedArgs(DebuggerResumeAction? resumeAction)
        {
            ResumeAction = resumeAction;
        }

        DebuggerResumeAction? ResumeAction { get; }
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
            context.PushPowerShell(runspace);

            Runspace.DefaultRunspace = runspace;
            psHost.RegisterRunspace(runspace);

            return context;
        }

        private readonly Stack<ContextFrame> _frameStack;

        private PowerShellContext()
        {
            _frameStack = new Stack<ContextFrame>();
        }

        public SMA.PowerShell CurrentPowerShell
        {
            get => _frameStack.Count > 0 ? _frameStack.Peek().PowerShell : null;
        }

        public event Action<object, PromptFramePushedArgs> PromptFramePushed;

        public event Action<object, PromptFramePoppedArgs> PromptFramePopped;

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;

        public event Action<object, DebuggerResumedArgs> DebuggerResumed;

        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        public void ProcessDebuggerResult(DebuggerCommandResults result)
        {
            DebuggerResumed?.Invoke(this, new DebuggerResumedArgs(result.ResumeAction));
        }

        public void PushNestedPowerShell()
        {
            // PowerShell.CreateNestedPowerShell() sets IsNested but not IsChild
            // So we must use the RunspaceMode.CurrentRunspace option on PowerShell.Create() instead
            var pwsh = SMA.PowerShell.Create(RunspaceMode.CurrentRunspace);
            pwsh.Runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;

            PushFrame(new ContextFrame(pwsh, PromptFrameType.NestedPrompt));
        }

        public void PopPowerShell()
        {
            PopFrame();
        }

        public void Dispose()
        {
            while (_frameStack.Count > 0)
            {
                _frameStack.Pop().PowerShell.Dispose();
            }
        }

        private void PushPowerShell(Runspace runspace)
        {
            var pwsh = SMA.PowerShell.Create();
            pwsh.Runspace = runspace;

            PushFrame(new ContextFrame(pwsh, PromptFrameType.Normal));
        }

        private void PushFrame(ContextFrame frame)
        {
            _frameStack.Push(frame);
            frame.PowerShell.Runspace.Debugger.DebuggerStop += OnDebuggerStopped;
            frame.PowerShell.Runspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
            PromptFramePushed?.Invoke(this, new PromptFramePushedArgs(frame.FrameType));
        }

        private void PopFrame()
        {
            ContextFrame frame = _frameStack.Pop();
            frame.PowerShell.Runspace.Debugger.DebuggerStop -= OnDebuggerStopped;
            frame.PowerShell.Runspace.Debugger.BreakpointUpdated -= OnBreakpointUpdated;
            PromptFramePopped?.Invoke(this, new PromptFramePoppedArgs(frame.FrameType));
            frame.PowerShell.Dispose();
        }

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs args)
        {
            DebuggerStopped?.Invoke(this, args);
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs args)
        {
            BreakpointUpdated?.Invoke(this, args);
        }

        private class ContextFrame
        {
            public ContextFrame(SMA.PowerShell powerShell, PromptFrameType frameType)
            {
                PowerShell = powerShell;
                FrameType = frameType;
            }

            public SMA.PowerShell PowerShell { get; }

            public PromptFrameType FrameType { get; }
        }
    }
}
