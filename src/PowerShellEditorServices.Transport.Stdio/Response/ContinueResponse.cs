using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("continue")]
    public class ContinueResponse : ResponseBase<object>
    {
    }
}
