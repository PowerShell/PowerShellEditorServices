using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    [MessageTypeName("terminated")]
    public class TerminatedEvent : EventBase<object>
    {
    }
}
