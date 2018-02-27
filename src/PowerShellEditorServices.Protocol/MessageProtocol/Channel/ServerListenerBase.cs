//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public abstract class ServerListenerBase<TChannel> : IServerListener
        where TChannel : ChannelBase
    {
        private MessageProtocolType messageProtocolType;

        public ServerListenerBase(MessageProtocolType messageProtocolType)
        {
            this.messageProtocolType = messageProtocolType;
        }

        public abstract void Start();

        public abstract void Stop();

        public event EventHandler<ChannelBase> ClientConnect;

        protected void OnClientConnect(TChannel channel)
        {
            channel.Start(this.messageProtocolType);
            this.ClientConnect?.Invoke(this, channel);
        }
    }

    public interface IServerListener
    {
        void Start();

        void Stop();

        event EventHandler<ChannelBase> ClientConnect;
    }
}