// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;

using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.PowerShell.EditorServices.Handlers;

/// <summary>
/// A handler for <a href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_prepareRename">textDocument/prepareRename</a>
/// </summary>
internal class PrepareRenameHandler
(
    RenameService renameService
) : IPrepareRenameHandler
{
    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) => capability.PrepareSupport ? new() { PrepareProvider = true } : new();

    public async Task<RangeOrPlaceholderRange?> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
        => await renameService.PrepareRenameSymbol(request, cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// A handler for <a href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_rename">textDocument/rename</a>
/// </summary>
internal class RenameHandler(
    RenameService renameService
) : IRenameHandler
{
    // RenameOptions may only be specified if the client states that it supports prepareSupport in its initial initialize request.
    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) => capability.PrepareSupport ? new() { PrepareProvider = true } : new();

    public async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
        => await renameService.RenameSymbol(request, cancellationToken).ConfigureAwait(false);
}
