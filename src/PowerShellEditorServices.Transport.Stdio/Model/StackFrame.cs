
using Microsoft.PowerShell.EditorServices.Console;
namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Model
{
    public class StackFrame
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public Source Source { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }

        public Scope[] Scopes { get; set; }

        //        /** An identifier for the stack frame. */
        //id: number;
        ///** The name of the stack frame, typically a method name */
        //name: string;
        ///** The source of the frame. */
        //source: Source;
        ///** The line within the file of the frame. */
        //line: number;
        ///** The column within the line. */
        //column: number;
        ///** All arguments and variables declared in this stackframe. */
        //scopes: Scope[];

        public static StackFrame Create(
            StackFrameDetails stackFrame)
        {
            return new StackFrame
            {
                Id = stackFrame.FunctionName.GetHashCode(),
                Name = stackFrame.FunctionName,
                Line = stackFrame.LineNumber,
                Column = stackFrame.ColumnNumber,
                Source = new Source
                {
                    Path = stackFrame.ScriptPath
                },
                Scopes = new Scope[]
                {
                    new Scope
                    {
                        Name = "locals",
                        VariablesReference = 1 // TODO: Use a contextual number
                    }
                }
            };
        }
    }
}
