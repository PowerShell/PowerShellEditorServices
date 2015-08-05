using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("launch")]
    public class LaunchResponse : ResponseBase<object>
    {
    }
}
