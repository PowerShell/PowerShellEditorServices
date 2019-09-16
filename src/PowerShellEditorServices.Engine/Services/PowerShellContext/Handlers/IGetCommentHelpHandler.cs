//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Engine.Handlers
{
    [Serial, Method("powerShell/getCommentHelp")]
    public interface IGetCommentHelpHandler : IJsonRpcRequestHandler<CommentHelpRequestParams, CommentHelpRequestResult> { }

    public class CommentHelpRequestResult
    {
        public string[] Content { get; set; }
    }

    public class CommentHelpRequestParams : IRequest<CommentHelpRequestResult>
    {
        public string DocumentUri { get; set; }
        public Position TriggerPosition { get; set; }
        public bool BlockComment { get; set; }
    }
}
