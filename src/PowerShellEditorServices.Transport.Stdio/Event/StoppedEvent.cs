using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    [MessageTypeName("stopped")]
    public class StoppedEvent : EventBase<StoppedEventBody>
    {
    }

    public class StoppedEventBody
    {
        /// <summary>
        /// A value such as "step", "breakpoint", "exception", or "pause"
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the current thread ID, if any.
        /// </summary>
        public int? ThreadId { get; set; }

        public Source Source { get; set; } 

        public int Line { get; set; }

        public int Column { get; set; }

        /// <summary>
        /// Gets or sets additional information such as an error message.
        /// </summary>
        public string Text { get; set; }
    }
}
