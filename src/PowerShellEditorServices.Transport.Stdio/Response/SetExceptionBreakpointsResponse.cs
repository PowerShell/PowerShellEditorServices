using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("setExceptionBreakpoints")]
    public class SetExceptionBreakpointsResponse : ResponseBase<object>
    {
    }
}
