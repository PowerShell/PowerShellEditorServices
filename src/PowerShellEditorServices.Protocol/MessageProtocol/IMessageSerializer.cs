//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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

