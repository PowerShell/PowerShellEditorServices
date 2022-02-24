// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.PowerShell.EditorServices.Extensions;

namespace Microsoft.PowerShell.EditorServices.Services.Extension
{
    internal class InvokeExtensionCommandHandler : IInvokeExtensionCommandHandler
    {
        private readonly ExtensionService _extensionService;
        private readonly EditorOperationsService _editorOperationsService;

        public InvokeExtensionCommandHandler(
            ExtensionService extensionService,
            EditorOperationsService editorOperationsService)
        {
            _extensionService = extensionService;
            _editorOperationsService = editorOperationsService;
        }

        public async Task<Unit> Handle(InvokeExtensionCommandParams request, CancellationToken cancellationToken)
        {
            EditorContext editorContext = _editorOperationsService.ConvertClientEditorContext(request.Context);
            await _extensionService.InvokeCommandAsync(request.Name, editorContext, cancellationToken).ConfigureAwait(false);
            return Unit.Value;
        }
    }
}
