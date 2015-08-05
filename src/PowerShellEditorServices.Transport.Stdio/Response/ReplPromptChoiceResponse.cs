//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("replPromptChoice")]
    public class ReplPromptChoiceResponse : ResponseBase<ReplPromptChoiceResponseBody>, IMessageProcessor
    {
        public Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            //editorSession.ConsoleService.ReceiveChoicePromptResult(
            //    0,  // TODO: Need to pass prompt ID!
            //    this.Body.Choice);

            return TaskConstants.Completed;
        }
    }

    public class ReplPromptChoiceResponseBody
    {
        public int Choice { get; set; }
    }
}
