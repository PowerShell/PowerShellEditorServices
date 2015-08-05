
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    // NOTE: Not clear what message type name this has based on protocol...
    [MessageTypeName("error")]
    public class ErrorResponse : ResponseBase<ErrorResponseBody>
    {
    }

    public class ErrorResponseBody
    {
        public string Error { get; set; }
    }
}
