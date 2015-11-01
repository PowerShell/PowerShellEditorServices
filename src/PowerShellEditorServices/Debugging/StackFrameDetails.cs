//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Contains details pertaining to a single stack frame in
    /// the current debugging session.
    /// </summary>
    public class StackFrameDetails
    {
        /// <summary>
        /// Gets the path to the script where the stack frame occurred.
        /// </summary>
        public string ScriptPath { get; private set; }

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
        /// Creates an instance of the StackFrameDetails class from a
        /// CallStackFrame instance provided by the PowerShell engine.
        /// </summary>
        /// <param name="callStackFrame">
        /// The original CallStackFrame instance from which details will be obtained.
        /// </param>
        /// <returns>A new instance of the StackFrameDetails class.</returns>
        static internal StackFrameDetails Create(
            CallStackFrame callStackFrame)
        {
            Dictionary<string, PSVariable> localVariables =
                callStackFrame.GetFrameVariables();

            return new StackFrameDetails
            {
                ScriptPath = callStackFrame.ScriptName,
                FunctionName = callStackFrame.FunctionName,
                LineNumber = callStackFrame.Position.StartLineNumber,
                ColumnNumber = callStackFrame.Position.StartColumnNumber
            };
        }
    }
}
