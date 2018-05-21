//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class StdioServerListener : ServerListenerBase<StdioServerChannel>
    {
        private PsesLogger logger;

        public StdioServerListener(
            MessageProtocolType messageProtocolType,
            PsesLogger logger)
                : base(messageProtocolType)
        {
            this.logger = logger;
        }

        public override void Start()
        {
            // Client is connected immediately because stdio
            // will buffer all I/O until we get to it
            this.OnClientConnect(
                new StdioServerChannel(
                    this.logger));
        }

        public override void Stop()
        {
        }
    }
}
