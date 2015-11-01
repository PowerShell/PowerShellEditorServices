//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices
{
    public class DebugService
    {
        #region Fields

        private PowerShellSession powerShellSession;
        private ConsoleServicePSHost consoleServicePSHost;

        // TODO: This needs to be managed per nested session
        private Dictionary<string, List<Breakpoint>> breakpointsPerFile = 
            new Dictionary<string, List<Breakpoint>>();

        private int nextVariableId;
        private List<VariableDetails> currentVariables;
        private StackFrameDetails[] callStackFrames;

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
                await this.ClearBreakpointsInFile(scriptFile);
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
            VariableDetails[] childVariables = null;

            if (variableReferenceId >= VariableDetails.FirstVariableId)
            {
                int correctedId =
                    (variableReferenceId - VariableDetails.FirstVariableId);

                VariableDetails parentVariable = 
                    this.currentVariables[correctedId];

                if (parentVariable.IsExpandable)
                {
                    childVariables = parentVariable.GetChildren();

                    foreach (var child in childVariables)
                    {
                        this.currentVariables.Add(child);
                        child.Id = this.nextVariableId;
                        this.nextVariableId++;
                    }
                }
                else
                {
                    childVariables = new VariableDetails[0];
                }
            }
            else
            {
                // TODO: Get variables for the desired scope ID
                childVariables = this.currentVariables.ToArray();
            }

            return childVariables;
        }

        public VariableDetails EvaluateExpression(string expressionString, int stackFrameId)
        {
            // Break up the variable path
            string[] variablePathParts = expressionString.Split('.');

            VariableDetails resolvedVariable = null;
            IEnumerable<VariableDetails> variableList = this.currentVariables;

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
                    resolvedVariable.IsExpandable)
                {
                    // Continue by searching in this variable's children
                    variableList = this.GetVariables(resolvedVariable.Id);
                }
            }

            return resolvedVariable;
        }

        public StackFrameDetails[] GetStackFrames()
        {
            return this.callStackFrames;
        }

        public VariableScope[] GetVariableScopes(int stackFrameId)
        {
            // TODO: Return different scopes based on PowerShell scoping mechanics
            return new VariableScope[]
            {
                new VariableScope(1, "Locals")
            };
        }

        #endregion

        #region Private Methods

        private async Task ClearBreakpointsInFile(ScriptFile scriptFile)
        {
            List<Breakpoint> breakpoints = null;

            // Get the list of breakpoints for this file
            if (this.breakpointsPerFile.TryGetValue(scriptFile.Id, out breakpoints))
            {
                if (breakpoints.Count > 0)
                {
                    PSCommand psCommand = new PSCommand();
                    psCommand.AddCommand("Remove-PSBreakpoint");
                    psCommand.AddParameter("Breakpoint", breakpoints.ToArray());

                    await this.powerShellSession.ExecuteCommand<object>(psCommand);

                    // Clear the existing breakpoints list for the file
                    breakpoints.Clear();
                }
            }
        }

        private async Task FetchVariables()
        {
            this.nextVariableId = VariableDetails.FirstVariableId;
            this.currentVariables = new List<VariableDetails>();

            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand("Get-Variable");
            psCommand.AddParameter("Scope", "Local");

            var results = await this.powerShellSession.ExecuteCommand<PSVariable>(psCommand);

            foreach (var variable in results)
            {
                var details = new VariableDetails(variable);
                details.Id = this.nextVariableId;
                this.currentVariables.Add(details);

                this.nextVariableId++;
            }
        }

        private async Task FetchStackFrames()
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand("Get-PSCallStack");

            var results = await this.powerShellSession.ExecuteCommand<CallStackFrame>(psCommand);

            this.callStackFrames =
                results
                    .Select(StackFrameDetails.Create)
                    .ToArray();
        }

        #endregion

        #region Events

        public event EventHandler<DebuggerStopEventArgs> DebuggerStopped;

        private async void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            // Get the call stack and local variables
            await this.FetchStackFrames();
            await this.FetchVariables();

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

            // Normalize the script filename for proper indexing
            string normalizedScriptName = e.Breakpoint.Script.ToLower();

            // Get the list of breakpoints for this file
            if (!this.breakpointsPerFile.TryGetValue(normalizedScriptName, out breakpoints))
            {
                breakpoints = new List<Breakpoint>();
                this.breakpointsPerFile.Add(
                    normalizedScriptName,
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
