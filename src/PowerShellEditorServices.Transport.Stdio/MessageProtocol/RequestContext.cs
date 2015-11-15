using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class RequestContext<TResult, TError>
    {
        private Message requestMessage;
        private MessageWriter messageWriter;

        public RequestContext(Message requestMessage, MessageWriter messageWriter)
        {
            this.requestMessage = requestMessage;
            this.messageWriter = messageWriter;
        }

        public async Task SendResult(TResult resultDetails)
        {
            await this.messageWriter.WriteResponse<TResult>(
                resultDetails,
                requestMessage.Method,
                requestMessage.Id);
        }

        public async Task SendEvent<TParams>(EventType<TParams> eventType, TParams eventParams)
        {
            await this.messageWriter.WriteEvent(
                eventType,
                eventParams);
        }

        public async Task SendError(TError errorDetails)
        {
            await this.messageWriter.WriteMessage(
                Message.ResponseError(
                    requestMessage.Id,
                    requestMessage.Method,
                    JToken.FromObject(errorDetails)));
        }
    }
}
