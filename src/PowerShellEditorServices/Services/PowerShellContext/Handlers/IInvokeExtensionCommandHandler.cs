//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/invokeExtensionCommand")]
    internal interface IInvokeExtensionCommandHandler : IJsonRpcNotificationHandler<InvokeExtensionCommandParams> { }

    internal class InvokeExtensionCommandParams : IRequest
    {
        public string Name { get; set; }

        public ClientEditorContext Context { get; set; }
    }

    internal class ClientEditorContext
    {
        public string CurrentFileContent { get; set; }

        public string CurrentFileLanguage { get; set; }

        public string CurrentFilePath { get; set; }

        public Position CursorPosition { get; set; }

        public Range SelectionRange { get; set; }

    }
}
