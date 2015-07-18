
namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    /// <summary>
    /// Indentifies the type of a given message.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// The message type is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The message is a request.
        /// </summary>
        Request,

        /// <summary>
        /// The message is a response.
        /// </summary>
        Response,

        /// <summary>
        /// The message is an event.
        /// </summary>
        Event
    }

}
