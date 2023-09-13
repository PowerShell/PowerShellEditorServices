// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

using Debugger = System.Management.Automation.Debugger;

namespace Microsoft.PowerShell.EditorServices;

internal record SyncedBreakpoint(
    ClientBreakpoint Client,
    Breakpoint Server);

internal enum SyncedBreakpointKind
{
    Line,

    Variable,

    Command,
}

internal sealed class BreakpointSyncService
{
    private record BreakpointMap(
        ConcurrentDictionary<string, SyncedBreakpoint> ByGuid,
        ConcurrentDictionary<int, SyncedBreakpoint> ByPwshId)
    {
        public void Register(SyncedBreakpoint breakpoint)
        {
            ByGuid.AddOrUpdate(
                breakpoint.Client.Id!,
                breakpoint,
                (_, _) => breakpoint);

            ByPwshId.AddOrUpdate(
                breakpoint.Server.Id,
                breakpoint,
                (_, _) => breakpoint);
        }

        public void Unregister(SyncedBreakpoint breakpoint)
        {
            ByGuid.TryRemove(breakpoint.Client.Id, out _);
            ByPwshId.TryRemove(breakpoint.Server.Id, out _);
        }
    }

    private record BreakpointTranslationInfo(
        SyncedBreakpointKind Kind,
        ScriptBlock? Action = null,
        string? Name = null,
        VariableAccessMode VariableMode = default,
        string? FilePath = null,
        int Line = 0,
        int Column = 0);

    internal record struct BreakpointHandle(SemaphoreSlim Target) : IDisposable
    {
        public void Dispose() => Target.Release();
    }

    private readonly BreakpointMap _map = new(new(), new());

    private readonly ConditionalWeakTable<Runspace, BreakpointMap> _attachedRunspaceMap = new();

    private readonly ConcurrentBag<SyncedBreakpoint> _pendingAdds = new();

    private readonly ConcurrentBag<SyncedBreakpoint> _pendingRemovals = new();

    private readonly SemaphoreSlim _breakpointMutateHandle = AsyncUtils.CreateSimpleLockingSemaphore();

    private readonly IInternalPowerShellExecutionService _host;

    private readonly ILogger<BreakpointSyncService> _logger;

    private readonly DebugStateService _debugStateService;

    private readonly ILanguageServerFacade _languageServer;

    private bool? _isSupported;

    public BreakpointSyncService(
        IInternalPowerShellExecutionService host,
        ILoggerFactory loggerFactory,
        DebugStateService debugStateService,
        ILanguageServerFacade languageServer)
    {
        _host = host;
        _logger = loggerFactory.CreateLogger<BreakpointSyncService>();
        _debugStateService = debugStateService;
        _languageServer = languageServer;
    }

    public bool IsSupported
    {
        get => _isSupported ?? false;
        set
        {
            Debug.Assert(_isSupported is null, "BreakpointSyncService.IsSupported should only be set once by initialize.");
            _isSupported = value;
        }
    }

    public bool IsMutatingBreakpoints
    {
        get
        {
            if (_breakpointMutateHandle.Wait(0))
            {
                _breakpointMutateHandle.Release();
                return false;
            }

            return true;
        }
    }

    private int? TargetRunspaceId => _debugStateService?.RunspaceId;

    public async Task UpdatedByServerAsync(BreakpointUpdatedEventArgs eventArgs)
    {
        if (!_map.ByPwshId.TryGetValue(eventArgs.Breakpoint.Id, out SyncedBreakpoint existing))
        {
            // If we haven't told the client about the breakpoint yet then we can just ignore.
            if (eventArgs.UpdateType is BreakpointUpdateType.Removed)
            {
                return;
            }

            existing = CreateFromServerBreakpoint(eventArgs.Breakpoint);
            string? id = await SendToClientAsync(existing.Client, BreakpointUpdateType.Set)
                .ConfigureAwait(false);

            existing.Client.Id = id!;
            RegisterBreakpoint(existing);
            return;
        }

        if (eventArgs.UpdateType is BreakpointUpdateType.Removed)
        {
            UnregisterBreakpoint(existing);
            await SendToClientAsync(existing.Client, BreakpointUpdateType.Removed).ConfigureAwait(false);
            return;
        }

        if (eventArgs.UpdateType is BreakpointUpdateType.Enabled or BreakpointUpdateType.Disabled)
        {
            await SendToClientAsync(existing.Client, eventArgs.UpdateType).ConfigureAwait(false);
            bool isActive = eventArgs.UpdateType is BreakpointUpdateType.Enabled;
            SyncedBreakpoint newBreakpoint = existing with
            {
                Client = existing.Client with
                {
                    Enabled = isActive,
                },
            };

            UnregisterBreakpoint(existing);
            RegisterBreakpoint(newBreakpoint);
            return;
        }

        LogBreakpointError(
            existing,
            "Somehow we're syncing a new breakpoint that we've already sync'd. That's not supposed to happen.");
    }

    public IReadOnlyList<SyncedBreakpoint> GetSyncedBreakpoints() => _map.ByGuid.Values.ToArray();

    public bool TryGetBreakpointByClientId(string id, [MaybeNullWhen(false)] out SyncedBreakpoint? syncedBreakpoint)
        => _map.ByGuid.TryGetValue(id, out syncedBreakpoint);

    public bool TryGetBreakpointByServerId(int id, [MaybeNullWhen(false)] out SyncedBreakpoint? syncedBreakpoint)
        => _map.ByPwshId.TryGetValue(id, out syncedBreakpoint);

    public void SyncServerAfterAttach()
    {
        if (!BreakpointApiUtils.SupportsBreakpointApis(_host.CurrentRunspace))
        {
            return;
        }

        bool ownsHandle = false;
        try
        {
            ownsHandle = _breakpointMutateHandle.Wait(0);
            Runspace runspace = _host.CurrentRunspace.Runspace;
            Debugger debugger = runspace.Debugger;
            foreach (Breakpoint existingBp in BreakpointApiUtils.GetBreakpointsDelegate(debugger, TargetRunspaceId))
            {
                BreakpointApiUtils.RemoveBreakpointDelegate(debugger, existingBp, TargetRunspaceId);
            }

            SyncedBreakpoint[] currentBreakpoints = _map.ByGuid.Values.ToArray();
            BreakpointApiUtils.SetBreakpointsDelegate(
                debugger,
                currentBreakpoints.Select(sbp => sbp.Server),
                TargetRunspaceId);

            BreakpointMap map = GetMapForRunspace(runspace);
            foreach (SyncedBreakpoint sbp in currentBreakpoints)
            {
                map.Register(sbp);
            }
        }
        finally
        {
            if (ownsHandle)
            {
                _breakpointMutateHandle.Release();
            }
        }
    }

    public void SyncServerAfterRunspacePop()
    {
        if (!BreakpointApiUtils.SupportsBreakpointApis(_host.CurrentRunspace))
        {
            return;
        }

        bool ownsHandle = false;
        try
        {
            ownsHandle = _breakpointMutateHandle.Wait(0);
            Debugger debugger = _host.CurrentRunspace.Runspace.Debugger;
            while (_pendingRemovals.TryTake(out SyncedBreakpoint sbp))
            {
                if (!_map.ByGuid.TryGetValue(sbp.Client.Id!, out SyncedBreakpoint existing))
                {
                    continue;
                }

                BreakpointApiUtils.RemoveBreakpointDelegate(debugger, existing.Server, null);
                _map.Unregister(existing);
            }

            BreakpointApiUtils.SetBreakpointsDelegate(
                debugger,
                _pendingAdds.Select(sbp => sbp.Server),
                null);

            while (_pendingAdds.TryTake(out SyncedBreakpoint sbp))
            {
                _map.Register(sbp);
            }
        }
        finally
        {
            if (ownsHandle)
            {
                _breakpointMutateHandle.Release();
            }
        }
    }

    public async Task UpdatedByClientAsync(
        IReadOnlyList<ClientBreakpoint>? clientBreakpoints,
        CancellationToken cancellationToken = default)
    {
        if (clientBreakpoints is null or { Count: 0 })
        {
            return;
        }

        using BreakpointHandle _ = await StartMutatingBreakpointsAsync(cancellationToken)
            .ConfigureAwait(false);

        List<(ClientBreakpoint Client, BreakpointTranslationInfo? Translation)>? toAdd = null;
        List<SyncedBreakpoint>? toRemove = null;
        List<(SyncedBreakpoint Breakpoint, bool Enabled)>? toSetActive = null;

        foreach (ClientBreakpoint clientBreakpoint in clientBreakpoints)
        {
            if (!_map.ByGuid.TryGetValue(clientBreakpoint.Id, out SyncedBreakpoint existing))
            {
                (toAdd ??= new()).Add((clientBreakpoint, GetTranslationInfo(clientBreakpoint)));
                continue;
            }

            if (clientBreakpoint == existing.Client)
            {
                continue;
            }

            // If the only thing that has changed is whether the breakpoint is enabled
            // we can skip removing and re-adding.
            if (clientBreakpoint.Enabled != existing.Client.Enabled
                && clientBreakpoint with { Enabled = existing.Client.Enabled } != existing.Client)
            {
                (toSetActive ??= new()).Add((existing, clientBreakpoint.Enabled));
                continue;
            }

            (toAdd ??= new()).Add((clientBreakpoint, GetTranslationInfo(clientBreakpoint)));
            (toRemove ??= new()).Add(existing);
        }

        await ExecuteDelegateAsync(
            "SyncUpdatedClientBreakpoints",
            executionOptions: null,
            (cancellationToken) =>
            {
                if (toRemove is not null and not { Count: 0 })
                {
                    foreach (SyncedBreakpoint bpToRemove in toRemove)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        UnsafeRemoveBreakpoint(
                            bpToRemove,
                            cancellationToken);
                    }
                }

                if (toAdd is not null and not { Count: 0 })
                {
                    foreach ((ClientBreakpoint Client, BreakpointTranslationInfo? Translation) in toAdd)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        UnsafeCreateBreakpoint(
                            Client,
                            Translation,
                            cancellationToken);
                    }
                }

                if (toSetActive is not null and not { Count: 0 })
                {
                    foreach ((SyncedBreakpoint Breakpoint, bool Enabled) bpToSetActive in toSetActive)
                    {
                        UnsafeSetBreakpointActive(
                            bpToSetActive.Enabled,
                            bpToSetActive.Breakpoint.Server,
                            cancellationToken);

                        SyncedBreakpoint newBreakpoint = bpToSetActive.Breakpoint with
                        {
                            Client = bpToSetActive.Breakpoint.Client with
                            {
                                Enabled = bpToSetActive.Enabled,
                            },
                        };

                        UnregisterBreakpoint(bpToSetActive.Breakpoint);
                        RegisterBreakpoint(newBreakpoint);
                    }
                }
            },
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemovedFromClientAsync(
        IReadOnlyList<ClientBreakpoint>? clientBreakpoints,
        CancellationToken cancellationToken = default)
    {
        if (clientBreakpoints is null or { Count: 0 })
        {
            return;
        }

        using BreakpointHandle _ = await StartMutatingBreakpointsAsync(cancellationToken)
            .ConfigureAwait(false);

        List<SyncedBreakpoint> syncedBreakpoints = new(clientBreakpoints.Count);
        foreach (ClientBreakpoint clientBreakpoint in clientBreakpoints)
        {
            // If we don't have a record of the breakpoint, we don't need to remove it.
            if (_map.ByGuid.TryGetValue(clientBreakpoint.Id!, out SyncedBreakpoint? syncedBreakpoint))
            {
                syncedBreakpoints.Add(syncedBreakpoint!);
            }
        }

        await ExecuteDelegateAsync(
            "SyncRemovedClientBreakpoints",
            executionOptions: null,
            (cancellationToken) =>
            {
                foreach (SyncedBreakpoint syncedBreakpoint in syncedBreakpoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    UnsafeRemoveBreakpoint(
                        syncedBreakpoint,
                        cancellationToken);
                }
            },
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task FromClientAsync(
        IReadOnlyList<ClientBreakpoint> clientBreakpoints,
        CancellationToken cancellationToken = default)
    {
        using BreakpointHandle _ = await StartMutatingBreakpointsAsync(cancellationToken)
            .ConfigureAwait(false);

        List<(ClientBreakpoint Client, BreakpointTranslationInfo? Translation)> breakpointsToCreate = new(clientBreakpoints.Count);
        foreach (ClientBreakpoint clientBreakpoint in clientBreakpoints)
        {
            breakpointsToCreate.Add((clientBreakpoint, GetTranslationInfo(clientBreakpoint)));
        }

        await ExecuteDelegateAsync(
            "SyncNewClientBreakpoints",
            executionOptions: null,
            (cancellationToken) =>
            {
                foreach ((ClientBreakpoint Client, BreakpointTranslationInfo? Translation) in breakpointsToCreate)
                {
                    UnsafeCreateBreakpoint(
                        Client,
                        Translation,
                        cancellationToken);
                }
            },
            cancellationToken)
            .ConfigureAwait(false);

        return;
    }

    private void LogBreakpointError(SyncedBreakpoint breakpoint, string message)
    {
        // Switch to the new C# 11 string literal syntax once it makes it to stable.
        _logger.LogError(
            "{Message}\n" +
            "    Server:\n" +
            "        Id: {ServerId}\n" +
            "        Enabled: {ServerEnabled}\n" +
            "        Action: {Action}\n" +
            "        Script: {Script}\n" +
            "        Command: {Command}\n" +
            "        Variable: {Variable}\n" +
            "    Client:\n" +
            "        Id: {ClientId}\n" +
            "        Enabled: {ClientEnabled}\n" +
            "        Condition: {Condition}\n" +
            "        HitCondition: {HitCondition}\n" +
            "        LogMessage: {LogMessage}\n" +
            "        Location: {Location}\n" +
            "        Function: {Function}\n",
            message,
            breakpoint.Server.Id,
            breakpoint.Server.Enabled,
            breakpoint.Server.Action,
            breakpoint.Server.Script,
            (breakpoint.Server as CommandBreakpoint)?.Command,
            (breakpoint.Server as VariableBreakpoint)?.Variable,
            breakpoint.Client.Id,
            breakpoint.Client.Enabled,
            breakpoint.Client.Condition,
            breakpoint.Client.HitCondition,
            breakpoint.Client.LogMessage,
            breakpoint.Client.Location,
            breakpoint.Client.FunctionName);
    }

    private async Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions? executionOptions,
            Action<CancellationToken> action,
            CancellationToken cancellationToken)
    {
        if (BreakpointApiUtils.SupportsBreakpointApis(_host.CurrentRunspace))
        {
            action(cancellationToken);
            return;
        }

        await _host.ExecuteDelegateAsync(
            representation,
            executionOptions,
            action,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private static SyncedBreakpoint CreateFromServerBreakpoint(Breakpoint serverBreakpoint)
    {
        string? functionName = null;
        ClientLocation? location = null;
        if (serverBreakpoint is VariableBreakpoint vbp)
        {
            string mode = vbp.AccessMode switch
            {
                VariableAccessMode.Read => "R",
                VariableAccessMode.Write => "W",
                _ => "RW",
            };

            functionName = $"${vbp.Variable}!{mode}";
        }
        else if (serverBreakpoint is CommandBreakpoint cbp)
        {
            functionName = cbp.Command;
        }
        else if (serverBreakpoint is LineBreakpoint lbp)
        {
            location = new ClientLocation(
                // TODO: fix the translation of this
                lbp.Script,
                new Range(
                    lbp.Line - 1,
                    Math.Max(lbp.Column - 1, 0),
                    lbp.Line - 1,
                    Math.Max(lbp.Column - 1, 0)));
        }

        ClientBreakpoint clientBreakpoint = new(
            serverBreakpoint.Enabled,
            serverBreakpoint.Action?.ToString(),
            null,
            null,
            location,
            functionName);

        SyncedBreakpoint syncedBreakpoint = new(clientBreakpoint, serverBreakpoint);
        return syncedBreakpoint;
    }

    private async Task<string?> SendToClientAsync(
        ClientBreakpoint clientBreakpoint,
        BreakpointUpdateType updateType)
    {
        if (updateType is BreakpointUpdateType.Set)
        {
            if (_languageServer is null)
            {
                return null;
            }

            // We need to send new breakpoints across as a separate request so we can get the client
            // ID back.
            return await _languageServer.SendRequest(
                "powerShell/setBreakpoint",
                clientBreakpoint)
                .Returning<string>(default)
                .ConfigureAwait(false);
        }

        bool isUpdate = updateType is BreakpointUpdateType.Enabled or BreakpointUpdateType.Disabled;
        bool isRemove = updateType is BreakpointUpdateType.Removed;
        _languageServer?.SendNotification(
            "powerShell/breakpointsChanged",
            new ClientBreakpointsChangedEvents(
                Array.Empty<ClientBreakpoint>(),
                isRemove ? new[] { clientBreakpoint } : Array.Empty<ClientBreakpoint>(),
                isUpdate ? new[] { clientBreakpoint } : Array.Empty<ClientBreakpoint>()));

        return null;
    }

    private Breakpoint? UnsafeCreateBreakpoint(
        BreakpointTranslationInfo translationInfo,
        CancellationToken cancellationToken)
    {
        if (BreakpointApiUtils.SupportsBreakpointApis(_host.CurrentRunspace))
        {
            return translationInfo.Kind switch
            {
                SyncedBreakpointKind.Command => BreakpointApiUtils.SetCommandBreakpointDelegate(
                    _host.CurrentRunspace.Runspace.Debugger,
                    translationInfo.Name,
                    translationInfo.Action,
                    string.Empty,
                    TargetRunspaceId),
                SyncedBreakpointKind.Variable => BreakpointApiUtils.SetVariableBreakpointDelegate(
                    _host.CurrentRunspace.Runspace.Debugger,
                    translationInfo.Name,
                    translationInfo.VariableMode,
                    translationInfo.Action,
                    string.Empty,
                    TargetRunspaceId),
                SyncedBreakpointKind.Line => BreakpointApiUtils.SetLineBreakpointDelegate(
                    _host.CurrentRunspace.Runspace.Debugger,
                    translationInfo.FilePath,
                    translationInfo.Line,
                    translationInfo.Column,
                    translationInfo.Action,
                    TargetRunspaceId),
                _ => throw new ArgumentOutOfRangeException(nameof(translationInfo)),
            };
        }

        PSCommand command = new PSCommand().AddCommand("Microsoft.PowerShell.Utility\\Set-PSBreakpoint");
        _ = translationInfo.Kind switch
        {
            SyncedBreakpointKind.Command => command.AddParameter("Command", translationInfo.Name),
            SyncedBreakpointKind.Variable => command
                .AddParameter("Variable", translationInfo.Name)
                .AddParameter("Mode", translationInfo.VariableMode),
            SyncedBreakpointKind.Line => command
                .AddParameter("Script", translationInfo.FilePath)
                .AddParameter("Line", translationInfo.Line),
            _ => throw new ArgumentOutOfRangeException(nameof(translationInfo)),
        };

        if (translationInfo is { Kind: SyncedBreakpointKind.Line, Column: > 0 })
        {
            command.AddParameter("Column", translationInfo.Column);
        }

        return _host.UnsafeInvokePSCommand<Breakpoint>(command, null, cancellationToken)
            is IReadOnlyList<Breakpoint> bp and { Count: 1 }
                ? bp[0]
                : null;
    }

    private BreakpointTranslationInfo? GetTranslationInfo(ClientBreakpoint clientBreakpoint)
    {
        if (clientBreakpoint.Location?.Uri is DocumentUri uri
            && !ScriptFile.IsUntitledPath(uri.ToString())
            && !PathUtils.HasPowerShellScriptExtension(uri.GetFileSystemPath()))
        {
            return null;
        }

        ScriptBlock? actionScriptBlock = null;

        // Check if this is a "conditional" line breakpoint.
        if (!string.IsNullOrWhiteSpace(clientBreakpoint.Condition) ||
            !string.IsNullOrWhiteSpace(clientBreakpoint.HitCondition) ||
            !string.IsNullOrWhiteSpace(clientBreakpoint.LogMessage))
        {
            actionScriptBlock = BreakpointApiUtils.GetBreakpointActionScriptBlock(
                clientBreakpoint.Condition,
                clientBreakpoint.HitCondition,
                clientBreakpoint.LogMessage,
                out string errorMessage);

            if (errorMessage is not null and not "")
            {
                _logger.LogError(
                    "Unable to create breakpoint action ScriptBlock with error message: \"{Error}\"\n" +
                    "    Condition: {Condition} \n" +
                    "    HitCondition: {HitCondition}\n" +
                    "    LogMessage: {LogMessage}\n",
                    errorMessage,
                    clientBreakpoint.Condition,
                    clientBreakpoint.HitCondition,
                    clientBreakpoint.LogMessage);
                return null;
            }
        }

        string? functionName = null;
        string? variableName = null;
        string? script = null;
        int line = 0, column = 0;
        VariableAccessMode mode = default;
        SyncedBreakpointKind kind = default;

        if (clientBreakpoint.FunctionName is not null)
        {
            ReadOnlySpan<char> functionSpan = clientBreakpoint.FunctionName.AsSpan();
            if (functionSpan is { Length: > 1 } && functionSpan[0] is '$')
            {
                ReadOnlySpan<char> nameWithSuffix = functionSpan[1..];
                mode = ParseSuffix(nameWithSuffix, out ReadOnlySpan<char> name);
                variableName = name.ToString();
                kind = SyncedBreakpointKind.Variable;
            }
            else
            {
                functionName = clientBreakpoint.FunctionName;
                kind = SyncedBreakpointKind.Command;
            }
        }

        if (clientBreakpoint.Location is not null)
        {
            kind = SyncedBreakpointKind.Line;
            script = clientBreakpoint.Location.Uri.Scheme is "untitled"
                ? clientBreakpoint.Location.Uri.ToString()
                : clientBreakpoint.Location.Uri.GetFileSystemPath();
            line = clientBreakpoint.Location.Range.Start.Line + 1;
            column = clientBreakpoint.Location.Range.Start.Character is int c and not 0 ? c : 0;
        }

        return new BreakpointTranslationInfo(
            kind,
            actionScriptBlock,
            kind is SyncedBreakpointKind.Variable ? variableName : functionName,
            mode,
            script,
            line,
            column);

        static VariableAccessMode ParseSuffix(ReadOnlySpan<char> nameWithSuffix, out ReadOnlySpan<char> name)
        {
            // TODO: Simplify `if` logic when list patterns from C# 11 make it to stable.
            //
            // if (nameWithSuffix is [..ReadOnlySpan<char> rest, '!', 'R' or 'r', 'W' or 'w'])
            if (nameWithSuffix is { Length: > 3 }
                && nameWithSuffix[^1] is 'w' or 'W'
                && nameWithSuffix[^2] is 'r' or 'R'
                && nameWithSuffix[^3] is '!')
            {
                name = nameWithSuffix[0..^3];
                return VariableAccessMode.ReadWrite;
            }

            // if (nameWithSuffix is [..ReadOnlySpan<char> rest1, '!', 'W' or 'w', 'R' or 'r'])
            if (nameWithSuffix is { Length: > 3 }
                && nameWithSuffix[^1] is 'r' or 'R'
                && nameWithSuffix[^2] is 'w' or 'W'
                && nameWithSuffix[^3] is '!')
            {
                name = nameWithSuffix[0..^3];
                return VariableAccessMode.ReadWrite;
            }

            // if (nameWithSuffix is [..ReadOnlySpan<char> rest2, '!', 'R' or 'r'])
            if (nameWithSuffix is { Length: > 2 }
                && nameWithSuffix[^1] is 'r' or 'R'
                && nameWithSuffix[^2] is '!')
            {
                name = nameWithSuffix[0..^2];
                return VariableAccessMode.Read;
            }

            // if (nameWithSuffix is [..ReadOnlySpan<char> rest3, '!', 'W' or 'w'])
            if (nameWithSuffix is { Length: > 2 }
                && nameWithSuffix[^1] is 'w' or 'W'
                && nameWithSuffix[^2] is '!')
            {
                name = nameWithSuffix[0..^2];
                return VariableAccessMode.Write;
            }

            name = nameWithSuffix;
            return VariableAccessMode.ReadWrite;
        }
    }

    internal BreakpointHandle StartMutatingBreakpoints(CancellationToken cancellationToken = default)
    {
        _breakpointMutateHandle.Wait(cancellationToken);
        return new BreakpointHandle(_breakpointMutateHandle);
    }

    private async Task<BreakpointHandle> StartMutatingBreakpointsAsync(CancellationToken cancellationToken = default)
    {
        await _breakpointMutateHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new BreakpointHandle(_breakpointMutateHandle);
    }

    private void UnsafeRemoveBreakpoint(
        SyncedBreakpoint breakpoint,
        CancellationToken cancellationToken)
    {
        UnsafeRemoveBreakpoint(
            breakpoint.Server,
            cancellationToken);

        UnregisterBreakpoint(breakpoint);
    }

    private void UnsafeRemoveBreakpoint(
        Breakpoint breakpoint,
        CancellationToken cancellationToken)
    {
        if (BreakpointApiUtils.SupportsBreakpointApis(_host.CurrentRunspace))
        {
            BreakpointApiUtils.RemoveBreakpointDelegate(
                _host.CurrentRunspace.Runspace.Debugger,
                breakpoint,
                TargetRunspaceId);
            return;
        }

        _host.UnsafeInvokePSCommand(
            new PSCommand()
                .AddCommand("Microsoft.PowerShell.Utility\\Remove-PSBreakpoint")
                .AddParameter("Id", breakpoint.Id),
            executionOptions: null,
            cancellationToken);
    }

    private Breakpoint? UnsafeSetBreakpointActive(
        bool enabled,
        Breakpoint? breakpoint,
        CancellationToken cancellationToken)
    {
        if (breakpoint is null)
        {
            return breakpoint;
        }

        if (BreakpointApiUtils.SupportsBreakpointApis(_host.CurrentRunspace))
        {
            if (enabled)
            {
                return BreakpointApiUtils.EnableBreakpointDelegate(
                    _host.CurrentRunspace.Runspace.Debugger,
                    breakpoint,
                    TargetRunspaceId);
            }

            return BreakpointApiUtils.DisableBreakpointDelegate(
                _host.CurrentRunspace.Runspace.Debugger,
                breakpoint,
                TargetRunspaceId);
        }

        string commandToInvoke = enabled
            ? "Microsoft.PowerShell.Utility\\Enable-PSBreakpoint"
            : "Microsoft.PowerShell.Utility\\Disable-PSBreakpoint";

        _host.UnsafeInvokePSCommand(
            new PSCommand()
                .AddCommand(commandToInvoke)
                .AddParameter("Id", breakpoint.Id),
            executionOptions: null,
            cancellationToken);

        return breakpoint;
    }

    private SyncedBreakpoint? UnsafeCreateBreakpoint(
        ClientBreakpoint client,
        BreakpointTranslationInfo? translation,
        CancellationToken cancellationToken)
    {
        if (translation is null or { Kind: SyncedBreakpointKind.Command, Name: null or "" })
        {
            return null;
        }

        Breakpoint? pwshBreakpoint = UnsafeCreateBreakpoint(
            translation,
            cancellationToken);

        if (!client.Enabled)
        {
            pwshBreakpoint = UnsafeSetBreakpointActive(
                false,
                pwshBreakpoint,
                cancellationToken);
        }

        if (pwshBreakpoint is null)
        {
            return null;
        }

        SyncedBreakpoint syncedBreakpoint = new(client, pwshBreakpoint);
        RegisterBreakpoint(syncedBreakpoint);
        return syncedBreakpoint;
    }

    private void RegisterBreakpoint(SyncedBreakpoint breakpoint)
    {
        BreakpointMap map = _map;
        if (_host.IsRunspacePushed)
        {
            map = GetMapForRunspace(_host.CurrentRunspace.Runspace);
            _pendingAdds.Add(breakpoint);
        }

        map.Register(breakpoint);
    }

    private BreakpointMap GetMapForRunspace(Runspace runspace)
        => _attachedRunspaceMap.GetValue(
                runspace,
                _ => new BreakpointMap(
                    new ConcurrentDictionary<string, SyncedBreakpoint>(),
                    new ConcurrentDictionary<int, SyncedBreakpoint>()));

    private void UnregisterBreakpoint(SyncedBreakpoint breakpoint)
    {
        BreakpointMap map = _map;
        if (_host.IsRunspacePushed)
        {
            map = GetMapForRunspace(_host.CurrentRunspace.Runspace);
            _pendingRemovals.Add(breakpoint);
        }

        map.Unregister(breakpoint);
    }
}
