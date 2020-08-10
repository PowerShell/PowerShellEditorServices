using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using System;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;
using System.Text;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    internal class PowerShellContext : IPowerShellContext
    {
        private readonly Stack<PowerShellContextFrame> _psFrameStack;

        public PowerShellContext()
        {
            _psFrameStack = new Stack<PowerShellContextFrame>();
        }

        public SMA.PowerShell CurrentPowerShell => _psFrameStack.Peek().PowerShell;

        private void PushFrame(PowerShellContextFrame frame)
        {
            if (_psFrameStack.Count > 0)
            {
                RemoveRunspaceEventHandlers(CurrentPowerShell.Runspace);
            }
            AddRunspaceEventHandlers(frame.PowerShell.Runspace);
            _psFrameStack.Push(frame);
            RunPowerShellLoop(frame.FrameType);
        }

        private void AddRunspaceEventHandlers(Runspace runspace)
        {
            runspace.Debugger.DebuggerStop += OnDebuggerStopped;
            runspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
            runspace.StateChanged += OnRunspaceStateChanged;
        }

        private void RemoveRunspaceEventHandlers(Runspace runspace)
        {
            runspace.Debugger.DebuggerStop -= OnDebuggerStopped;
            runspace.Debugger.BreakpointUpdated -= OnBreakpointUpdated;
            runspace.StateChanged -= OnRunspaceStateChanged;
        }
    }
}
