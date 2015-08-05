using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices
{
    public class DebugService
    {
        #region Fields

        private PowerShellSession powerShellSession;
        private ConsoleServicePSHost consoleServicePSHost;

        private Dictionary<string, List<Breakpoint>> breakpointsPerFile = 
            new Dictionary<string, List<Breakpoint>>();
        private StackFrameDetails[] callStackFrames;
        private VariableDetails[] currentVariables;
        private Dictionary<int, VariableDetails> variableIndex;

        #endregion

        #region Constructors

        public DebugService(PowerShellSession powerShellSession)
        {
            Validate.IsNotNull("powerShellSession", powerShellSession);

            this.powerShellSession = powerShellSession;
            this.powerShellSession.DebuggerStop += this.OnDebuggerStop;
            this.powerShellSession.BreakpointUpdated += this.OnBreakpointUpdated;
        }

        #endregion

        #region Public Methods

        public async Task<BreakpointDetails[]> SetBreakpoints(
            ScriptFile scriptFile, 
            int[] lineNumbers, 
            bool clearExisting = true)
        {
            IEnumerable<Breakpoint> resultBreakpoints = null;

            if (clearExisting)
            {
                this.ClearBreakpointsInFile(scriptFile);
            }

            if (lineNumbers.Length > 0)
            {
                PSCommand psCommand = new PSCommand();
                psCommand.AddCommand("Set-PSBreakpoint");
                psCommand.AddParameter("Script", scriptFile.FilePath);
                psCommand.AddParameter("Line", lineNumbers.Length > 0 ? lineNumbers : null);
                resultBreakpoints =
                    await this.powerShellSession.ExecuteCommand<Breakpoint>(
                        psCommand);

                return
                    resultBreakpoints
                        .Select(BreakpointDetails.Create)
                        .ToArray();
            }

            return new BreakpointDetails[0];
        }


        public void Continue()
        {
            this.powerShellSession.ResumeDebugger(
                DebuggerResumeAction.Continue);
        }

        public void StepOver()
        {
            this.powerShellSession.ResumeDebugger(
                DebuggerResumeAction.StepOver);
        }

        public void StepIn()
        {
            this.powerShellSession.ResumeDebugger(
                DebuggerResumeAction.StepInto);
        }

        public void StepOut()
        {
            this.powerShellSession.ResumeDebugger(
                DebuggerResumeAction.StepOut);
        }

        public void Break()
        {
            // Break execution in the debugger
            this.powerShellSession.BreakExecution();
        }

        public void Stop()
        {
            this.powerShellSession.AbortExecution();
        }

        public VariableDetails[] GetVariables(int variableReferenceId)
        {
            var variables = this.currentVariables;

            if (variableReferenceId != 1)
            {
                VariableDetails variable = null;
                if (this.variableIndex.TryGetValue(variableReferenceId, out variable))
                {
                    if (variable.HasChildren)
                    {
                        variables = variable.Children;
                    }
                    else
                    {
                        // TODO: Throw error
                    }
                }
            }

            return variables;
        }

        public VariableDetails EvaluateExpression(string expressionString, int stackFrameId)
        {
            // Break up the variable path
            string[] variablePathParts = expressionString.Split('.');

            VariableDetails resolvedVariable = null;
            VariableDetails[] variableList = this.currentVariables;

            foreach (var variableName in variablePathParts)
            {
                if (variableList == null)
                {
                    // If there are no children left to search, break out early
                    return null;
                }

                resolvedVariable =
                    variableList.FirstOrDefault(
                        v =>
                            string.Equals(
                                v.Name,
                                expressionString,
                                StringComparison.InvariantCultureIgnoreCase));

                if (resolvedVariable != null && 
                    resolvedVariable.HasChildren)
                {
                    // Continue by searching in this variable's children
                    variableList = resolvedVariable.Children;
                }
            }

            return resolvedVariable;
        }

        public StackFrameDetails[] GetStackFrames()
        {
            return this.callStackFrames;
        }

        #endregion

        #region Private Methods

        private void ClearBreakpointsInFile(ScriptFile scriptFile)
        {
            List<Breakpoint> breakpoints = null;

            // Get the list of breakpoints for this file
            if (this.breakpointsPerFile.TryGetValue(scriptFile.FilePath, out breakpoints))
            {
                if (breakpoints.Count > 0)
                {
                    PSCommand psCommand = new PSCommand();
                    psCommand.AddCommand("Remove-PSBreakpoint");
                    psCommand.AddParameter("Breakpoint", breakpoints.ToArray());

                    this.powerShellSession.ExecuteCommand<object>(psCommand);
                }
            }
        }

        private async Task<VariableDetails[]> FetchVariables()
        {
            this.variableIndex = new Dictionary<int, VariableDetails>();

            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand("Get-Variable");

            var results = await this.powerShellSession.ExecuteCommand<PSVariable>(psCommand);

            VariableDetails[] variables =
                results
                    .Select(VariableDetails.Create)
                    .ToArray();

            // TODO: Do this more efficiently
            foreach (var variable in variables)
            {
                this.variableIndex.Add(variable.Id, variable);
            }

            return variables;
        }

        private async Task<StackFrameDetails[]> FetchStackFrames()
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand("Get-PSCallStack");

            var results = await this.powerShellSession.ExecuteCommand<CallStackFrame>(psCommand);

            StackFrameDetails[] stackFrames =
                results
                    .Select(StackFrameDetails.Create)
                    .ToArray();

            return stackFrames;
        }

        #endregion

        #region Events

        public event EventHandler<DebuggerStopEventArgs> DebuggerStopped;

        private async void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            // Get the call stack and local variables
            this.callStackFrames = await this.FetchStackFrames();
            this.currentVariables = await this.FetchVariables();

            // Notify the host that the debugger is stopped
            if (this.DebuggerStopped != null)
            {
                this.DebuggerStopped(sender, e);
            }
        }

        public event EventHandler<BreakpointUpdatedEventArgs> BreakpointUpdated;

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            List<Breakpoint> breakpoints = null;

            // Get the list of breakpoints for this file
            if (!this.breakpointsPerFile.TryGetValue(e.Breakpoint.Script, out breakpoints))
            {
                breakpoints = new List<Breakpoint>();
                this.breakpointsPerFile.Add(
                    e.Breakpoint.Script,
                    breakpoints);
            }

            // Add or remove the breakpoint based on the update type
            if (e.UpdateType == BreakpointUpdateType.Set)
            {
                breakpoints.Add(e.Breakpoint);
            }
            else if(e.UpdateType == BreakpointUpdateType.Removed)
            {
                breakpoints.Remove(e.Breakpoint);
            }
            else
            {
                // TODO: Do I need to switch out instances for updated breakpoints?
            }

            if (this.BreakpointUpdated != null)
            {
                this.BreakpointUpdated(sender, e);
            }
        }

        #endregion
    }
}
