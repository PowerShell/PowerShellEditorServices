using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Owin;
using Owin.WebSocket.Extensions;

namespace PowerShellEditorServices.Host.Iis
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapWebSocketRoute<LanguageServerWebSocketConnection>("/language");
            app.MapWebSocketRoute<DebugAdapterWebSocketConnection>("/debug");
        }
    }
}