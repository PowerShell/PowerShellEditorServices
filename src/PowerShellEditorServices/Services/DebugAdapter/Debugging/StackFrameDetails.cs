// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Contains details pertaining to a single stack frame in
    /// the current debugging session.
    /// </summary>
    internal class StackFrameDetails
    {
        #region Fields

        /// <summary>
        /// A constant string used in the ScriptPath field to represent a
        /// stack frame with no associated script file.
        /// </summary>
        public const string NoFileScriptPath = "<No File>";

        #endregion

        #region Properties

        /// <summary>
        /// Gets the path to the script where the stack frame occurred.
        /// </summary>
        public string ScriptPath { get; internal set; }

        /// <summary>
        /// Gets the name of the function where the stack frame occurred.
        /// </summary>
        public string FunctionName { get; private set; }

        /// <summary>
        /// Gets the start line number of the script where the stack frame occurred.
        /// </summary>
        public int StartLineNumber { get; internal set; }

        /// <summary>
        /// Gets the line number of the script where the stack frame occurred.
        /// </summary>
        public int? EndLineNumber { get; internal set; }

        /// <summary>
        /// Gets the start column number of the line where the stack frame occurred.
        /// </summary>
        public int StartColumnNumber { get; internal set; }

        /// <summary>
        /// Gets the end column number of the line where the stack frame occurred.
        /// </summary>
        public int? EndColumnNumber { get; internal set; }

        /// <summary>
        /// Gets a boolean value indicating whether or not the stack frame is executing
        /// in script external to the current workspace root.
        /// </summary>
        public bool IsExternalCode { get; internal set; }

        /// <summary>
        /// Gets or sets the VariableContainerDetails that contains the auto variables.
        /// </summary>
        public VariableContainerDetails AutoVariables { get; private set; }

        /// <summary>
        /// Gets or sets the VariableContainerDetails that contains the call stack frame variables.
        /// </summary>
        public VariableContainerDetails CommandVariables { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the StackFrameDetails class from a
        /// CallStackFrame instance provided by the PowerShell engine.
        /// </summary>
        /// <param name="callStackFrameObject">
        /// A PSObject representing the CallStackFrame instance from which details will be obtained.
        /// </param>
        /// <param name="autoVariables">
        /// A variable container with all the filtered, auto variables for this stack frame.
        /// </param>
        /// <param name="commandVariables">
        /// A variable container with all the command variables for this stack frame.
        /// </param>
        /// <returns>A new instance of the StackFrameDetails class.</returns>
        internal static StackFrameDetails Create(
            PSObject callStackFrameObject,
            VariableContainerDetails autoVariables,
            VariableContainerDetails commandVariables)
        {
            string scriptPath = (callStackFrameObject.Properties["ScriptName"].Value as string) ?? NoFileScriptPath;
            int startLineNumber = (int)(callStackFrameObject.Properties["ScriptLineNumber"].Value ?? 0);

            return new StackFrameDetails
            {
                ScriptPath = scriptPath,
                FunctionName = callStackFrameObject.Properties["FunctionName"].Value as string,
                StartLineNumber = startLineNumber,
                EndLineNumber = startLineNumber, // End line number isn't given in PowerShell stack frames
                StartColumnNumber = 0, // Column number isn't given in PowerShell stack frames
                EndColumnNumber = 0,
                AutoVariables = autoVariables,
                CommandVariables = commandVariables,
                // TODO: Re-enable `isExternal` detection along with a setting. Will require
                // `workspaceRootPath`, see Git blame.
                IsExternalCode = false
            };
        }

        #endregion
    }
}
