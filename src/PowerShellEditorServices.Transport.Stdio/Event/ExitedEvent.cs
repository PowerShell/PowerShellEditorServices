using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    [MessageTypeName("exited")]
    public class ExitedEvent : EventBase<ExitedEventBody>
    {
    }

    public class ExitedEventBody
    {
        public int ExitCode { get; set; }
    }
}
