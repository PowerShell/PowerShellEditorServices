//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Contains details pertaining to a single stack frame in
    /// the current debugging session.
    /// </summary>
    public class StackFrameDetails
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
        /// Gets the line number of the script where the stack frame occurred.
        /// </summary>
        public int LineNumber { get; private set; }

        /// <summary>
        /// Gets the column number of the line where the stack frame occurred.
        /// </summary>
        public int ColumnNumber { get; private set; }

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
        /// <returns>A new instance of the StackFrameDetails class.</returns>
        static internal StackFrameDetails Create(
            PSObject callStackFrameObject,
            VariableContainerDetails autoVariables,
            VariableContainerDetails localVariables)
        {
            return new StackFrameDetails
            {
                ScriptPath = (callStackFrameObject.Properties["ScriptName"].Value as string) ?? NoFileScriptPath,
                FunctionName = callStackFrameObject.Properties["FunctionName"].Value as string,
                LineNumber = (int)(callStackFrameObject.Properties["ScriptLineNumber"].Value ?? 0),
                ColumnNumber = 0,   // Column number isn't given in PowerShell stack frames
                AutoVariables = autoVariables,
                LocalVariables = localVariables
            };
        }

        #endregion
    }
}
