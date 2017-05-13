//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    class CommentHelpRequest
    {
        public static readonly RequestType<CommentHelpRequestParams, CommentHelpRequestResult, object, object> Type
            = RequestType<CommentHelpRequestParams, CommentHelpRequestResult, object, object>.Create("powershell/getCommentHelp");
    }

    public class CommentHelpRequestResult
    {
        public string[] content;
    }

    public class CommentHelpRequestParams
    {
        public string DocumentUri { get; set; }
        public Position TriggerPosition { get; set; }
    }
}

