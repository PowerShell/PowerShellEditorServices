//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.PowerShell.EditorServices.Services.Analysis;
using Microsoft.PowerShell.EditorServices.Services.Configuration;
using System.Collections;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Provides a high-level service for performing semantic analysis
    /// of PowerShell scripts.
    /// </summary>
    internal class AnalysisService : IDisposable
    {
        internal static string GetUniqueIdFromDiagnostic(Diagnostic diagnostic)
        {
            Position start = diagnostic.Range.Start;
            Position end = diagnostic.Range.End;

            var sb = new StringBuilder(256)
            .Append(diagnostic.Source ?? "?")
            .Append("_")
            .Append(diagnostic.Code.IsString ? diagnostic.Code.String : diagnostic.Code.Long.ToString())
            .Append("_")
            .Append(diagnostic.Severity?.ToString() ?? "?")
            .Append("_")
            .Append(start.Line)
            .Append(":")
            .Append(start.Character)
            .Append("-")
            .Append(end.Line)
            .Append(":")
            .Append(end.Character);

            var id = sb.ToString();
            return id;
        }

        /// <summary>
        /// Defines the list of Script Analyzer rules to include by default if
        /// no settings file is specified.
        /// </summary>
        private static readonly string[] s_defaultRules = {
            "PSAvoidAssignmentToAutomaticVariable",
            "PSUseToExportFieldsInManifest",
            "PSMisleadingBacktick",
            "PSAvoidUsingCmdletAliases",
            "PSUseApprovedVerbs",
            "PSAvoidUsingPlainTextForPassword",
            "PSReservedCmdletChar",
            "PSReservedParams",
            "PSShouldProcess",
            "PSMissingModuleManifestField",
            "PSAvoidDefaultValueSwitchParameter",
            "PSUseDeclaredVarsMoreThanAssignments",
            "PSPossibleIncorrectComparisonWithNull",
            "PSAvoidDefaultValueForMandatoryParameter",
            "PSPossibleIncorrectUsageOfRedirectionOperator"
        };

        private readonly ILogger _logger;

        private readonly ILanguageServer _languageServer;

        private readonly ConfigurationService _configurationService;

        private readonly WorkspaceService _workplaceService;

        private readonly int _analysisDelayMillis;

        private readonly ConcurrentDictionary<string, (SemaphoreSlim, ConcurrentDictionary<string, MarkerCorrection>)> _mostRecentCorrectionsByFile;

        private IAnalysisEngine _analysisEngineField;

        private CancellationTokenSource _diagnosticsCancellationTokenSource;

        public AnalysisService(
            ILoggerFactory loggerFactory,
            ILanguageServer languageServer,
            ConfigurationService configurationService,
            WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<AnalysisService>();
            _languageServer = languageServer;
            _configurationService = configurationService;
            _workplaceService = workspaceService;
            _analysisDelayMillis = 750;
            _mostRecentCorrectionsByFile = new ConcurrentDictionary<string, (SemaphoreSlim, ConcurrentDictionary<string, MarkerCorrection>)>();
        }

        public string[] EnabledRules { get; set; }

        public string SettingsPath { get; set; }

        private IAnalysisEngine AnalysisEngine
        {
            get
            {
                if (_analysisEngineField == null)
                {
                    _analysisEngineField = InstantiateAnalysisEngine();
                }

                return _analysisEngineField;
            }
        }

        public Task RunScriptDiagnosticsAsync(
            ScriptFile[] filesToAnalyze,
            CancellationToken cancellationToken)
        {
            if (!AnalysisEngine.IsEnabled)
            {
                return Task.CompletedTask;
            }

            // Create a cancellation token source that will cancel if we do or if the caller does
            var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            CancellationTokenSource oldTaskCancellation;
            // If there's an existing task, we want to cancel it here
            if ((oldTaskCancellation = Interlocked.Exchange(ref _diagnosticsCancellationTokenSource, cancellationSource)) != null)
            {
                try
                {
                    oldTaskCancellation.Cancel();
                    oldTaskCancellation.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception occurred while cancelling analysis task");
                }
            }

            if (filesToAnalyze.Length == 0)
            {
                return Task.CompletedTask;
            }

            return Task.Run(() => DelayThenInvokeDiagnosticsAsync(filesToAnalyze, _diagnosticsCancellationTokenSource.Token), _diagnosticsCancellationTokenSource.Token);
        }

        public Task<string> FormatAsync(string scriptFileContents, Hashtable formatSettings, int[] formatRange = null)
        {
            if (!AnalysisEngine.IsEnabled)
            {
                return null;
            }

            return AnalysisEngine.FormatAsync(scriptFileContents, formatSettings, formatRange);
        }

        public async Task<string> GetCommentHelpText(string functionText, string helpLocation, bool forBlockComment)
        {
            if (!AnalysisEngine.IsEnabled)
            {
                return null;
            }

            Hashtable commentHelpSettings = AnalysisService.GetCommentHelpRuleSettings(helpLocation, forBlockComment);

            ScriptFileMarker[] analysisResults = await AnalysisEngine.AnalyzeScriptAsync(functionText, commentHelpSettings);

            if (analysisResults.Length == 0
                || analysisResults[0]?.Correction?.Edits == null
                || analysisResults[0].Correction.Edits.Count() == 0)
            {
                return null;
            }

            return analysisResults[0].Correction.Edits[0].Text;
        }

        public IReadOnlyDictionary<string, MarkerCorrection> GetMostRecentCodeActionsForFile(string documentUri)
        {
            if (!_mostRecentCorrectionsByFile.TryGetValue(documentUri, out (SemaphoreSlim fileLock, ConcurrentDictionary<string, MarkerCorrection> corrections) fileCorrectionsEntry))
            {
                return null;
            }

            return fileCorrectionsEntry.corrections;
        }

        public Task ClearMarkers(ScriptFile file)
        {
            return PublishScriptDiagnosticsAsync(file, Array.Empty<ScriptFileMarker>());
        }

        public void OnConfigurationUpdated(object sender, LanguageServerSettings settings)
        {
            ClearOpenFileMarkers();
            _analysisEngineField = InstantiateAnalysisEngine();
        }

        private IAnalysisEngine InstantiateAnalysisEngine()
        {
            if (_configurationService.CurrentSettings.ScriptAnalysis.Enable ?? false)
            {
                return new NullAnalysisEngine();
            }

            var pssaCmdletEngineBuilder = new PssaCmdletAnalysisEngine.Builder(_logger);

            if (TryFindSettingsFile(out string settingsFilePath))
            {
                _logger.LogInformation($"Configuring PSScriptAnalyzer with rules at '{settingsFilePath}'");
                pssaCmdletEngineBuilder.WithSettingsFile(settingsFilePath);
            }
            else
            {
                _logger.LogInformation("PSScriptAnalyzer settings file not found. Falling back to default rules");
                pssaCmdletEngineBuilder.WithIncludedRules(s_defaultRules);
            }

            return pssaCmdletEngineBuilder.Build();
        }

        private bool TryFindSettingsFile(out string settingsFilePath)
        {
            string configuredPath = _configurationService.CurrentSettings.ScriptAnalysis.SettingsPath;

            if (!string.IsNullOrEmpty(configuredPath))
            {
                settingsFilePath = _workplaceService.ResolveWorkspacePath(configuredPath);
                return settingsFilePath != null;
            }

            // TODO: Could search for a default here

            settingsFilePath = null;
            return false;
        }

        private Task ClearOpenFileMarkers()
        {
            return Task.WhenAll(
                _workplaceService.GetOpenedFiles()
                    .Select(file => ClearMarkers(file)));
        }

        private async Task DelayThenInvokeDiagnosticsAsync(ScriptFile[] filesToAnalyze, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(_analysisDelayMillis, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Ensure no stale markers are displayed
                foreach (ScriptFile script in filesToAnalyze)
                {
                    await PublishScriptDiagnosticsAsync(script).ConfigureAwait(false);
                }

                return;
            }

            // If we've made it past the delay period then we don't care
            // about the cancellation token anymore.  This could happen
            // when the user stops typing for long enough that the delay
            // period ends but then starts typing while analysis is going
            // on.  It makes sense to send back the results from the first
            // delay period while the second one is ticking away.

            foreach (ScriptFile scriptFile in filesToAnalyze)
            {
                ScriptFileMarker[] semanticMarkers = await AnalysisEngine.AnalyzeScriptAsync(scriptFile.Contents).ConfigureAwait(false);

                scriptFile.DiagnosticMarkers.AddRange(semanticMarkers);

                await PublishScriptDiagnosticsAsync(scriptFile).ConfigureAwait(false);
            }
        }

        private Task PublishScriptDiagnosticsAsync(ScriptFile scriptFile) => PublishScriptDiagnosticsAsync(scriptFile, scriptFile.DiagnosticMarkers);

        private async Task PublishScriptDiagnosticsAsync(ScriptFile scriptFile, IReadOnlyList<ScriptFileMarker> markers)
        {
            (SemaphoreSlim fileLock, ConcurrentDictionary<string, MarkerCorrection> fileCorrections) = _mostRecentCorrectionsByFile.GetOrAdd(
                scriptFile.DocumentUri,
                CreateFileCorrectionsEntry);

            var diagnostics = new Diagnostic[scriptFile.DiagnosticMarkers.Count];

            await fileLock.WaitAsync();
            try
            {
                fileCorrections.Clear();

                for (int i = 0; i < markers.Count; i++)
                {
                    ScriptFileMarker marker = markers[i];

                    Diagnostic diagnostic = GetDiagnosticFromMarker(marker);

                    if (marker.Correction != null)
                    {
                        string diagnosticId = GetUniqueIdFromDiagnostic(diagnostic);
                        fileCorrections[diagnosticId] = marker.Correction;
                    }

                    diagnostics[i] = diagnostic;
                }
            }
            finally
            {
                fileLock.Release();
            }

            _languageServer.Document.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = new Uri(scriptFile.DocumentUri),
                Diagnostics = new Container<Diagnostic>(diagnostics)
            });
        }

        private static (SemaphoreSlim, ConcurrentDictionary<string, MarkerCorrection>) CreateFileCorrectionsEntry(string fileUri)
        {
            return (AsyncUtils.CreateSimpleLockingSemaphore(), new ConcurrentDictionary<string, MarkerCorrection>());
        }

        private static Diagnostic GetDiagnosticFromMarker(ScriptFileMarker scriptFileMarker)
        {
            return new Diagnostic
            {
                Severity = MapDiagnosticSeverity(scriptFileMarker.Level),
                Message = scriptFileMarker.Message,
                Code = scriptFileMarker.RuleName,
                Source = scriptFileMarker.Source,
                Range = new Range
                {
                    Start = new Position
                    {
                        Line = scriptFileMarker.ScriptRegion.StartLineNumber - 1,
                        Character = scriptFileMarker.ScriptRegion.StartColumnNumber - 1
                    },
                    End = new Position
                    {
                        Line = scriptFileMarker.ScriptRegion.EndLineNumber - 1,
                        Character = scriptFileMarker.ScriptRegion.EndColumnNumber - 1
                    }
                }
            };
        }

        private static DiagnosticSeverity MapDiagnosticSeverity(ScriptFileMarkerLevel markerLevel)
        {
            switch (markerLevel)
            {
                case ScriptFileMarkerLevel.Error:
                    return DiagnosticSeverity.Error;

                case ScriptFileMarkerLevel.Warning:
                    return DiagnosticSeverity.Warning;

                case ScriptFileMarkerLevel.Information:
                    return DiagnosticSeverity.Information;

                default:
                    return DiagnosticSeverity.Error;
            }
        }

        private static Hashtable GetCommentHelpRuleSettings(string helpLocation, bool forBlockComment)
        {
            return new Hashtable {
                { "Rules", new Hashtable {
                    { "PSProvideCommentHelp", new Hashtable {
                        { "Enable", true },
                        { "ExportedOnly", false },
                        { "BlockComment", forBlockComment },
                        { "VSCodeSnippetCorrection", true },
                        { "Placement", helpLocation }}
                    }}
                }};
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _analysisEngineField?.Dispose();
                    _diagnosticsCancellationTokenSource?.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

    }
}
