// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Configuration;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

// Using an alias since this is conflicting with System.IO.FileSystemWatcher and ends up really finicky.
using OmniSharpFileSystemWatcher = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher;

namespace Microsoft.PowerShell.EditorServices.Handlers;

/// <summary>
/// Receives file change notifications from the client for any file in the workspace, including those
/// that are not considered opened by the client. This handler is used to allow us to scan the
/// workspace only once when the language server starts.
/// </summary>
internal class DidChangeWatchedFilesHandler : IDidChangeWatchedFilesHandler
{
    private readonly WorkspaceService _workspaceService;

    private readonly ConfigurationService _configurationService;

    public DidChangeWatchedFilesHandler(
        WorkspaceService workspaceService,
        ConfigurationService configurationService)
    {
        _workspaceService = workspaceService;
        _configurationService = configurationService;
    }

    public DidChangeWatchedFilesRegistrationOptions GetRegistrationOptions(
        DidChangeWatchedFilesCapability capability,
        ClientCapabilities clientCapabilities)
        => new()
        {
            Watchers = new[]
            {
                new OmniSharpFileSystemWatcher()
                {
                    GlobPattern = "**/*.{ps1,psm1}",
                    Kind = WatchKind.Create | WatchKind.Delete | WatchKind.Change,
                },
            },
        };

    public Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
    {
        LanguageServerSettings currentSettings = _configurationService.CurrentSettings;
        if (currentSettings.AnalyzeOpenDocumentsOnly)
        {
            return Task.FromResult(Unit.Value);
        }

        // Honor `search.exclude` settings in the watcher.
        Matcher matcher = new();
        matcher.AddExcludePatterns(_workspaceService.ExcludeFilesGlob);
        foreach (FileEvent change in request.Changes)
        {
            if (matcher.Match(change.Uri.GetFileSystemPath()).HasMatches)
            {
                continue;
            }

            if (!_workspaceService.TryGetFile(change.Uri, out ScriptFile scriptFile))
            {
                continue;
            }

            if (change.Type is FileChangeType.Created)
            {
                // We've already triggered adding the file to `OpenedFiles` via `TryGetFile`.
                continue;
            }

            if (change.Type is FileChangeType.Deleted)
            {
                _workspaceService.CloseFile(scriptFile);
                continue;
            }

            if (change.Type is FileChangeType.Changed)
            {
                // If the file is opened by the editor (rather than by us in the background), let
                // DidChangeTextDocument handle changes.
                if (scriptFile.IsOpen)
                {
                    continue;
                }

                string fileContents;
                try
                {
                    fileContents = _workspaceService.ReadFileContents(change.Uri);
                }
                catch
                {
                    continue;
                }

                scriptFile.SetFileContents(fileContents);
                scriptFile.References.TagAsChanged();
            }
        }

        return Task.FromResult(Unit.Value);
    }
}
