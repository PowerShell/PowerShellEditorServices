//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class StdioServerListener : ServerListenerBase<StdioServerChannel>
    {
        public StdioServerListener(MessageProtocolType messageProtocolType) :
            base(messageProtocolType)
        {
        }

        public override void Start()
        {
            // Client is connected immediately because stdio
            // will buffer all I/O until we get to it
            this.OnClientConnect(new StdioServerChannel());
        }

        public override void Stop()
        {
        }
    }
}
