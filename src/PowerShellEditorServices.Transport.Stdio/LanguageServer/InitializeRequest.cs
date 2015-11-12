//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Microsoft.PowerShell.EditorServices.Utility;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("initialize")]
    public class InitializeRequest : RequestBase<InitializeRequestArguments>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            // TODO: Remove this behavior in the near future --
            //   Create the debug service log in a separate file
            //   so that there isn't a conflict with the default 
            //   log file.
            Logger.Initialize("DebugService.log", LogLevel.Verbose);

            // Send the Initialized event first so that we get breakpoints
            await messageWriter.WriteMessage(
                new InitializedEvent());

            // Now send the Initialize response to continue setup
            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new InitializeResponse()));
        }
    }

    public class InitializeRequestArguments
    {
        public string AdapterId { get; set; }

        public bool LinesStartAt1 { get; set; }

        public string PathFormat { get; set; }

        public bool SourceMaps { get; set; }

        public string GeneratedCodeDirectory { get; set; }
    }
}
