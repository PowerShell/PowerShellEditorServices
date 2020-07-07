using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security.Cryptography;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
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

    internal class PowerShellEventService
    {
        private readonly Stack<PowerShellContextFrame> _frameStack;

        public PowerShellEventService()
        {
            _frameStack = new Stack<PowerShellContextFrame>();
        }

        public void PushFrame(Runspace runspace, PromptFrameType frameType)
        {
            UnregisterCurrentRunspace();

            runspace.Debugger.DebuggerStop += OnDebuggerStopped;
            runspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
            _frameStack.Push(new PowerShellContextFrame
            {
                Runspace = runspace,
                FrameType = frameType,
            });

            PromptFramePushed?.Invoke(this, new PromptFramePushedArgs(frameType));
        }

        public void PopFrame()
        {
            PromptFrameType frameType = PopAndDisposeCurrentRunspace();

            if (_frameStack.Count > 0)
            {
                PowerShellContextFrame currentFrame = _frameStack.Peek();
                currentFrame.Runspace.Debugger.DebuggerStop += OnDebuggerStopped;
                currentFrame.Runspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
            }

            PromptFramePopped?.Invoke(this, new PromptFramePoppedArgs(frameType));
        }

        public void ProcessDebuggerCommandResults(DebuggerCommandResults results)
        {
            DebuggerResumed?.Invoke(this, new DebuggerResumedArgs(results.ResumeAction));
        }

        public void Dispose()
        {
            while (_frameStack.Count > 0)
            {
                PopAndDisposeCurrentRunspace();
            }
        }

        public event Action<object, PromptFramePushedArgs> PromptFramePushed;

        public event Action<object, PromptFramePoppedArgs> PromptFramePopped;

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;

        public event Action<object, DebuggerResumedArgs> DebuggerResumed;

        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs args)
        {
            DebuggerStopped?.Invoke(this, args);
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs args)
        {
            BreakpointUpdated?.Invoke(this, args);
        }

        private void UnregisterCurrentRunspace()
        {
            if (_frameStack.Count > 0)
            {
                PowerShellContextFrame frame = _frameStack.Peek();
                UnregisterRunspace(frame.Runspace);
            }
        }

        private PromptFrameType PopAndDisposeCurrentRunspace()
        {
            PowerShellContextFrame frame = _frameStack.Pop();
            UnregisterRunspace(frame.Runspace);
            frame.Runspace.Dispose();
            return frame.FrameType;
        }

        private void UnregisterRunspace(Runspace runspace)
        {
            if (runspace.Debugger != null)
            {
                runspace.Debugger.DebuggerStop -= OnDebuggerStopped;
                runspace.Debugger.BreakpointUpdated -= OnBreakpointUpdated;
            }
        }

        private struct PowerShellContextFrame
        {
            public Runspace Runspace { get; set; }

            public PromptFrameType FrameType { get; set; }
        }
    }
}
