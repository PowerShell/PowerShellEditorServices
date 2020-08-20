// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getCommentHelp")]
    internal interface IGetCommentHelpHandler : IJsonRpcRequestHandler<CommentHelpRequestParams, CommentHelpRequestResult> { }

    internal class CommentHelpRequestResult
    {
        public string[] Content { get; set; }
    }

    internal class CommentHelpRequestParams : IRequest<CommentHelpRequestResult>
    {
        public string DocumentUri { get; set; }
        public Position TriggerPosition { get; set; }
        public bool BlockComment { get; set; }
    }
}
