using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("stackTrace")]
    public class StackTraceResponse : ResponseBase<StackTraceResponseBody>
    {
        public static StackTraceResponse Create(
            StackFrameDetails[] stackFrames)
        {
            List<StackFrame> newStackFrames = new List<StackFrame>();

            for (int i = 0; i < stackFrames.Length; i++)
            {
                // Create the new StackFrame object with an ID that can
                // be referenced back to the current list of stack frames
                newStackFrames.Add(
                    StackFrame.Create(
                        stackFrames[i], 
                        i + 1));
            }

            return new StackTraceResponse
            {
                Body = new StackTraceResponseBody
                {
                    StackFrames = newStackFrames.ToArray()
                }
            };
        }
    }

    public class StackTraceResponseBody
    {
        public StackFrame[] StackFrames { get; set; }
    }
}
