//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
        /// Gets or sets the VariableContainerDetails that contains the local variables.
        /// </summary>
        public VariableContainerDetails LocalVariables { get; private set; }

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
        /// <param name="localVariables">
        /// A variable container with all the local variables for this stack frame.
        /// </param>
        /// <param name="workspaceRootPath">
        /// Specifies the path to the root of an open workspace, if one is open. This path is used to
        /// determine whether individua stack frames are external to the workspace.
        /// </param>
        /// <returns>A new instance of the StackFrameDetails class.</returns>
        static internal StackFrameDetails Create(
            PSObject callStackFrameObject,
            VariableContainerDetails autoVariables,
            VariableContainerDetails localVariables,
            string workspaceRootPath = null)
        {
            string moduleId = string.Empty;
            var isExternal = false;

            var invocationInfo = callStackFrameObject.Properties["InvocationInfo"]?.Value as InvocationInfo;
            string scriptPath = (callStackFrameObject.Properties["ScriptName"].Value as string) ?? NoFileScriptPath;
            int startLineNumber = (int)(callStackFrameObject.Properties["ScriptLineNumber"].Value ?? 0);

            // TODO: RKH 2019-03-07 Temporarily disable "external" code until I have a chance to add
            // settings to control this feature.
            //if (workspaceRootPath != null &&
            //    invocationInfo != null &&
            //    !scriptPath.StartsWith(workspaceRootPath, StringComparison.OrdinalIgnoreCase))
            //{
            //    isExternal = true;
            //}

            return new StackFrameDetails
            {
                ScriptPath = scriptPath,
                FunctionName = callStackFrameObject.Properties["FunctionName"].Value as string,
                StartLineNumber = startLineNumber,
                EndLineNumber = startLineNumber, // End line number isn't given in PowerShell stack frames
                StartColumnNumber = 0,   // Column number isn't given in PowerShell stack frames
                EndColumnNumber = 0,
                AutoVariables = autoVariables,
                LocalVariables = localVariables,
                IsExternalCode = isExternal
            };
        }

        #endregion
    }
}
