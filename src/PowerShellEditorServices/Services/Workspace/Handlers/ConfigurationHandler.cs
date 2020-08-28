//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Configuration;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using System.IO;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.Extension;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesConfigurationHandler : IDidChangeConfigurationHandler
    {
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;
        private readonly ConfigurationService _configurationService;
        private readonly ExtensionService _extensionService;
        private readonly EditorServicesConsolePSHost _psesHost;
        private DidChangeConfigurationCapability _capability;
        private bool _profilesLoaded;
        private bool _consoleReplStarted;
        private bool _extensionServiceInitialized;
        private bool _cwdSet;

        public PsesConfigurationHandler(
            ILoggerFactory factory,
            WorkspaceService workspaceService,
            AnalysisService analysisService,
            ConfigurationService configurationService,
            ExtensionService extensionService,
            EditorServicesConsolePSHost psesHost)
        {
            _logger = factory.CreateLogger<PsesConfigurationHandler>();
            _workspaceService = workspaceService;
            _configurationService = configurationService;
            _extensionService = extensionService;
            _psesHost = psesHost;

            ConfigurationUpdated += analysisService.OnConfigurationUpdated;
        }

        public object GetRegistrationOptions()
        {
            return null;
        }

        public async Task<Unit> Handle(DidChangeConfigurationParams request, CancellationToken cancellationToken)
        {
            LanguageServerSettingsWrapper incomingSettings = request.Settings.ToObject<LanguageServerSettingsWrapper>();
            if(incomingSettings == null)
            {
                return await Unit.Task.ConfigureAwait(false);
            }

            bool oldLoadProfiles = _configurationService.CurrentSettings.EnableProfileLoading;
            bool oldScriptAnalysisEnabled =
                _configurationService.CurrentSettings.ScriptAnalysis.Enable ?? false;
            string oldScriptAnalysisSettingsPath =
                _configurationService.CurrentSettings.ScriptAnalysis?.SettingsPath;

            _configurationService.CurrentSettings.Update(
                incomingSettings.Powershell,
                _workspaceService.WorkspacePath,
                _logger);

            if (!_psesHost.IsRunning)
            {
                await _psesHost.StartAsync(new HostStartOptions
                {
                    LoadProfiles = _configurationService.CurrentSettings.EnableProfileLoading,
                }, CancellationToken.None).ConfigureAwait(false);
            }

            if (!this._cwdSet)
            {
                if (!string.IsNullOrEmpty(_configurationService.CurrentSettings.Cwd)
                    && Directory.Exists(_configurationService.CurrentSettings.Cwd))
                {
                    await _psesHost.SetInitialWorkingDirectoryAsync(
                        _configurationService.CurrentSettings.Cwd,
                        CancellationToken.None).ConfigureAwait(false);

                } else if (_workspaceService.WorkspacePath != null
                    && Directory.Exists(_workspaceService.WorkspacePath))
                {
                    await _psesHost.SetInitialWorkingDirectoryAsync(
                        _workspaceService.WorkspacePath,
                        CancellationToken.None).ConfigureAwait(false);
                }

                this._cwdSet = true;
            }

            if (!_extensionServiceInitialized)
            {
                await _extensionService.InitializeAsync();
            }

            // Run any events subscribed to configuration updates
            ConfigurationUpdated?.Invoke(this, _configurationService.CurrentSettings);

            // Convert the editor file glob patterns into an array for the Workspace
            // Both the files.exclude and search.exclude hash tables look like (glob-text, is-enabled):
            // "files.exclude" : {
            //     "Makefile": true,
            //     "*.html": true,
            //     "build/*": true
            // }
            var excludeFilePatterns = new List<string>();
            if (incomingSettings.Files?.Exclude != null)
            {
                foreach(KeyValuePair<string, bool> patternEntry in incomingSettings.Files.Exclude)
                {
                    if (patternEntry.Value) { excludeFilePatterns.Add(patternEntry.Key); }
                }
            }
            if (incomingSettings.Search?.Exclude != null)
            {
                foreach(KeyValuePair<string, bool> patternEntry in incomingSettings.Search.Exclude)
                {
                    if (patternEntry.Value && !excludeFilePatterns.Contains(patternEntry.Key)) { excludeFilePatterns.Add(patternEntry.Key); }
                }
            }
            _workspaceService.ExcludeFilesGlob = excludeFilePatterns;

            // Convert the editor file search options to Workspace properties
            if (incomingSettings.Search?.FollowSymlinks != null)
            {
                _workspaceService.FollowSymlinks = incomingSettings.Search.FollowSymlinks;
            }

            return await Unit.Task.ConfigureAwait(false);
        }

        public void SetCapability(DidChangeConfigurationCapability capability)
        {
            _capability = capability;
        }

        public event EventHandler<LanguageServerSettings> ConfigurationUpdated;
    }
}
