using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public interface IMessageSerializer
    {
        JObject SerializeMessage(Message message);

        Message DeserializeMessage(JObject messageJson);
    }
}
