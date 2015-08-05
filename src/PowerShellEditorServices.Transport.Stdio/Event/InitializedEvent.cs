using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    [MessageTypeName("initialized")]
    public class InitializedEvent : EventBase<object>
    {
    }
}