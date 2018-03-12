using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    /// <summary>
    /// Defines an event type for PowerShell context execution status changes (e.g. execution has completed)
    /// </summary>
    public class ExecutionStatusChangedEvent
    {
        /// <summary>
        /// The notification type for execution status change events in the message protocol
        /// </summary>
        /// <returns></returns>
        public static readonly
            NotificationType<object, object> Type =
            NotificationType<object, object>.Create("powerShell/executionStatusChanged");
    }
}
