//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices
{
    public class StackFrameDetails
    {
        public string ScriptPath { get; private set; }

        public string FunctionName { get; private set; }

        public int LineNumber { get; private set; }

        public int ColumnNumber { get; private set; }

        public Dictionary<string, PSVariable> LocalVariables { get; private set; }

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
                ColumnNumber = callStackFrame.Position.StartColumnNumber,
                LocalVariables = callStackFrame.GetFrameVariables()
            };
        }
    }
}
