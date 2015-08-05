
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("initialize")]
    public class InitializeResponse : ResponseBase<object>
    {
    }
}
