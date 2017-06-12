//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using LanguageServer = Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;

namespace Microsoft.PowerShell.EditorServices
{
    public static class ClientCommandExtensions
    {
        public static LanguageServer.ServerCommand ToProtocolCommand(
            this ClientCommand clientCommand,
            JsonSerializer jsonSerializer)
        {
            return new LanguageServer.ServerCommand
            {
                Command = clientCommand.Name,
                Title = clientCommand.Title,
                Arguments =
                    JArray.FromObject(
                        clientCommand.Arguments,
                        jsonSerializer)
            };
        }
    }
}
