using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
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
