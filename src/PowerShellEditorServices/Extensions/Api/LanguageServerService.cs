//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Extensions.Services
{
    /// <summary>
    /// Service allowing the sending of notifications and requests to the PowerShell LSP language client from the server.
    /// </summary>
    public interface ILanguageServerService
    {
        /// <summary>
        /// Send a parameterless notification.
        /// </summary>
        /// <param name="method">The method to send.</param>
        void SendNotification(string method);

        /// <summary>
        /// Send a notification with parameters.
        /// </summary>
        /// <typeparam name="T">The type of the parameter object.</typeparam>
        /// <param name="method">The method to send.</param>
        /// <param name="parameters">The parameters to send.</param>
        void SendNotification<T>(string method, T parameters);

        /// <summary>
        /// Send a parameterless request with no response output.
        /// </summary>
        /// <param name="method">The method to send.</param>
        /// <returns>A task that resolves when the request is acknowledged.</returns>
        Task SendRequestAsync(string method);

        /// <summary>
        /// Send a request with no response output.
        /// </summary>
        /// <typeparam name="T">The type of the request parameter object.</typeparam>
        /// <param name="method">The method to send.</param>
        /// <param name="parameters">The request parameter object/body.</param>
        /// <returns>A task that resolves when the request is acknowledged.</returns>
        Task SendRequestAsync<T>(string method, T parameters);

        /// <summary>
        /// Send a parameterless request and get its response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response expected.</typeparam>
        /// <param name="method">The method to send.</param>
        /// <returns>A task that resolves to the response sent by the server.</returns>
        Task<TResponse> SendRequestAsync<TResponse>(string method);

        /// <summary>
        /// Send a request and get its response.
        /// </summary>
        /// <typeparam name="T">The type of the parameter object.</typeparam>
        /// <typeparam name="TResponse">The type of the response expected.</typeparam>
        /// <param name="method">The method to send.</param>
        /// <param name="parameters">The parameters to send.</param>
        /// <returns>A task that resolves to the response sent by the server.</returns>
        Task<TResponse> SendRequestAsync<T, TResponse>(string method, T parameters);
    }

    internal class LanguageServerService : ILanguageServerService
    {
        private readonly ILanguageServer _languageServer;

        internal LanguageServerService(ILanguageServer languageServer)
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

        public Task SendRequestAsync(string method)
        {
            return _languageServer.SendRequest(method).ReturningVoid(CancellationToken.None);
        }

        public Task SendRequestAsync<T>(string method, T parameters)
        {
            return _languageServer.SendRequest<T>(method, parameters).ReturningVoid(CancellationToken.None);
        }

        public Task<TResponse> SendRequestAsync<TResponse>(string method)
        {
            return _languageServer.SendRequest(method).Returning<TResponse>(CancellationToken.None);
        }

        public Task<TResponse> SendRequestAsync<T, TResponse>(string method, T parameters)
        {
            return _languageServer.SendRequest<T>(method, parameters).Returning<TResponse>(CancellationToken.None);
        }

        public Task<object> SendRequestAsync(string method, object parameters)
        {
            return _languageServer.SendRequest<object>(method, parameters).Returning<object>(CancellationToken.None);
        }
    }
}
