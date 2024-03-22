// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Services.Extension
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
