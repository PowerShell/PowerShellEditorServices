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
using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ConfigurationHandler : IDidChangeConfigurationHandler
    {
        private readonly ILogger _logger;
        private readonly AnalysisService _analysisService;
        private readonly WorkspaceService _workspaceService;
        private readonly ConfigurationService _configurationService;
        private readonly PowerShellContextService _powerShellContextService;
        private DidChangeConfigurationCapability _capability;
        private bool _profilesLoaded;
        private bool _consoleReplStarted;

        public ConfigurationHandler(
            ILoggerFactory factory,
            WorkspaceService workspaceService,
            AnalysisService analysisService,
            ConfigurationService configurationService,
            PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<ConfigurationHandler>();
            _workspaceService = workspaceService;
            _analysisService = analysisService;
            _configurationService = configurationService;
            _powerShellContextService = powerShellContextService;
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
                return await Unit.Task;
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

            if (!this._profilesLoaded &&
                _configurationService.CurrentSettings.EnableProfileLoading &&
                oldLoadProfiles != _configurationService.CurrentSettings.EnableProfileLoading)
            {
                await _powerShellContextService.LoadHostProfilesAsync();
                this._profilesLoaded = true;
            }

            // Wait until after profiles are loaded (or not, if that's the
            // case) before starting the interactive console.
            if (!this._consoleReplStarted)
            {
                // Start the interactive terminal
                _powerShellContextService.ConsoleReader.StartCommandLoop();
                this._consoleReplStarted = true;
            }

            // If there is a new settings file path, restart the analyzer with the new settigs.
            bool settingsPathChanged = false;
            string newSettingsPath = _configurationService.CurrentSettings.ScriptAnalysis.SettingsPath;
            if (!string.Equals(oldScriptAnalysisSettingsPath, newSettingsPath, StringComparison.OrdinalIgnoreCase))
            {
                if (_analysisService != null)
                {
                    _analysisService.SettingsPath = newSettingsPath;
                    settingsPathChanged = true;
                }
            }

            // If script analysis settings have changed we need to clear & possibly update the current diagnostic records.
            if ((oldScriptAnalysisEnabled != _configurationService.CurrentSettings.ScriptAnalysis?.Enable) || settingsPathChanged)
            {
                // If the user just turned off script analysis or changed the settings path, send a diagnostics
                // event to clear the analysis markers that they already have.
                if (!_configurationService.CurrentSettings.ScriptAnalysis.Enable.Value || settingsPathChanged)
                {
                    foreach (var scriptFile in _workspaceService.GetOpenedFiles())
                    {
                        _analysisService.ClearMarkers(scriptFile);
                    }
                }
            }

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
                foreach(KeyValuePair<string, bool> patternEntry in incomingSettings.Files.Exclude)
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

            return await Unit.Task;
        }

        public void SetCapability(DidChangeConfigurationCapability capability)
        {
            _capability = capability;
        }
    }
}
