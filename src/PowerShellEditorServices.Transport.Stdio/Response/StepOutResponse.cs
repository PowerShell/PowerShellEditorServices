using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("stepOut")]
    public class StepOutResponse : ResponseBase<object>
    {
    }
}
