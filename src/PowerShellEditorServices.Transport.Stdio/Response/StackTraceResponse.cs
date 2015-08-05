using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("stackTrace")]
    public class StackTraceResponse : ResponseBase<StackTraceResponseBody>
    {
        public static StackTraceResponse Create(
            StackFrameDetails[] stackFrames)
        {
            return new StackTraceResponse
            {
                Body = new StackTraceResponseBody
                {
                    StackFrames =
                        stackFrames
                            .Select(StackFrame.Create)
                            .ToArray()
                }
            };
        }
    }

    public class StackTraceResponseBody
    {
        public StackFrame[] StackFrames { get; set; }
    }
}
