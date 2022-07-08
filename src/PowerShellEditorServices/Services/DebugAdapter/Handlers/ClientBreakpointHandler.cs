// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace Microsoft.PowerShell.EditorServices;

internal record ClientLocation(
    DocumentUri Uri,
    Range Range);

internal record ClientBreakpoint(
    bool Enabled,
    [property: Optional] string? Condition = null,
    [property: Optional] string? HitCondition = null,
    [property: Optional] string? LogMessage = null,
    [property: Optional] ClientLocation? Location = null,
    [property: Optional] string? FunctionName = null)
{
    private string? _id;

    // The ID needs to come from the client, so if the breakpoint is originating on the server
    // then we need to create an ID-less breakpoint to send over to the client.
    public string Id
    {
        get
        {
            Debug.Assert(
                _id is not null,
                "Caller should ensure ClientBreakpoint ID is retrieved by the client prior to calling this property.");

            return _id!;
        }
        set => _id = value;
    }
}

internal record ClientBreakpointsChangedEvents(
    Container<ClientBreakpoint> Added,
    Container<ClientBreakpoint> Removed,
    Container<ClientBreakpoint> Changed)
    : IRequest, IBaseRequest;

[Serial, Method("powerShell/breakpointsChanged", Direction.Bidirectional)]
internal interface IClientBreakpointHandler : IJsonRpcNotificationHandler<ClientBreakpointsChangedEvents>
{
}

internal sealed class ClientBreakpointHandler : IClientBreakpointHandler
{
    private readonly ILogger _logger;

    private readonly BreakpointSyncService _breakpoints;

    public ClientBreakpointHandler(
        ILoggerFactory loggerFactory,
        BreakpointSyncService breakpointSyncService)
    {
        _logger = loggerFactory.CreateLogger<ClientBreakpointHandler>();
        _breakpoints = breakpointSyncService;
    }

    public async Task<Unit> Handle(
        ClientBreakpointsChangedEvents request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting breakpoint sync initiated by client.");
        await _breakpoints.FromClientAsync(
            request.Added.ToArray(),
            cancellationToken)
            .ConfigureAwait(false);

        await _breakpoints.UpdatedByClientAsync(
            request.Changed.ToArray(),
            cancellationToken)
            .ConfigureAwait(false);

        await _breakpoints.RemovedFromClientAsync(
            request.Removed.ToArray(),
            cancellationToken)
            .ConfigureAwait(false);

        return new();
    }
}
