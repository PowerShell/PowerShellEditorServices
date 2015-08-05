using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("stepIn")]
    public class StepInResponse : ResponseBase<object>
    {
    }
}
