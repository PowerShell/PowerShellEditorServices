//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.CodeLenses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LanguageServer = Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;

namespace Microsoft.PowerShell.EditorServices
{
    public static class ICodeLensExtensions
    {
        public static LanguageServer.CodeLens ToProtocolCodeLens(
            this CodeLens codeLens,
            JsonSerializer jsonSerializer)
        {
            return new LanguageServer.CodeLens
            {
                Range = codeLens.ScriptExtent.ToRange(),
                Command = codeLens.Command.ToProtocolCommand(jsonSerializer)
            };
        }

        public static LanguageServer.CodeLens ToProtocolCodeLens(
            this CodeLens codeLens,
            object codeLensData,
            JsonSerializer jsonSerializer)
        {
            LanguageServer.ServerCommand command = null;

            if (codeLens.Command != null)
            {
                command = codeLens.Command.ToProtocolCommand(jsonSerializer);
            }

            return new LanguageServer.CodeLens
            {
                Range = codeLens.ScriptExtent.ToRange(),
                Data = JToken.FromObject(codeLensData, jsonSerializer),
                Command = command
            };
        }
    }
}
