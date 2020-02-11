using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    public class EditorLanguageServer
    {
        private readonly ILanguageServer _languageServer;

        internal EditorLanguageServer(ILanguageServer languageServer)
        {
            _languageServer = languageServer;
        }

        public void SendNotification(string method)
        {
            _languageServer.SendNotification(method);
        }

        public void SendNotification<T>(string method, T parameters)
        {
            _languageServer.SendNotification(method, parameters);
        }

        public void SendNotification(string method, object parameters)
        {
            _languageServer.SendNotification(method, parameters);
        }

        public Task<TResponse> SendRequestAsync<TResponse>(string method)
        {
            return _languageServer.SendRequest<TResponse>(method);
        }

        public Task<TResponse> SendRequestAsync<T, TResponse>(string method, T parameters)
        {
            return _languageServer.SendRequest<T, TResponse>(method, parameters);
        }

        public Task<object> SendRequestAsync(string method, object parameters)
        {
            return _languageServer.SendRequest<object, object>(method, parameters);
        }
    }
}
