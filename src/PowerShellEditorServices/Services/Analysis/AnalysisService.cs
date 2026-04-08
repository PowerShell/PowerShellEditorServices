// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.Analysis;
using Microsoft.PowerShell.EditorServices.Services.Configuration;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Provides a high-level service for performing semantic analysis
    /// of PowerShell scripts.
    /// </summary>
    internal class AnalysisService : IDisposable
    {
        /// <summary>
        /// Reliably generate an ID for a diagnostic record to track it.
        /// </summary>
        /// <param name="diagnostic">The diagnostic to generate an ID for.</param>
        /// <returns>A string unique to this diagnostic given where and what kind it is.</returns>
        internal static string GetUniqueIdFromDiagnostic(Diagnostic diagnostic)
        {
            Position start = diagnostic.Range.Start;
            Position end = diagnostic.Range.End;

            StringBuilder sb = new StringBuilder(256)
                .Append(diagnostic.Source ?? "?")
                .Append('_')
                .Append(diagnostic.Code?.IsString ?? true ? diagnostic.Code?.String : diagnostic.Code?.Long.ToString())
                .Append('_')
                .Append(diagnostic.Severity?.ToString() ?? "?")
                .Append('_')
                .Append(start.Line)
                .Append(':')
                .Append(start.Character)
                .Append('-')
                .Append(end.Line)
                .Append(':')
                .Append(end.Character);

            return sb.ToString();
        }

        /// <summary>
        /// Defines the list of Script Analyzer rules to include by default if
        /// no settings file is specified.
        /// </summary>
        internal static readonly string[] s_defaultRules = {
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

        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        private readonly ILanguageServerFacade _languageServer;

        private readonly ConfigurationService _configurationService;

        private readonly WorkspaceService _workspaceService;

        private readonly int _analysisDelayMillis = 750;

        private readonly ConcurrentDictionary<ScriptFile, CorrectionTableEntry> _mostRecentCorrectionsByFile = new();

        private Lazy<PssaCmdletAnalysisEngine> _analysisEngineLazy;

        private readonly string _pssaModulePath;

        private string _pssaSettingsFilePath;

        public AnalysisService(
            ILoggerFactory loggerFactory,
            ILanguageServerFacade languageServer,
            ConfigurationService configurationService,
            WorkspaceService workspaceService,
            HostStartupInfo hostInfo)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<AnalysisService>();
            _languageServer = languageServer;
            _configurationService = configurationService;
            _workspaceService = workspaceService;
            _analysisEngineLazy = new Lazy<PssaCmdletAnalysisEngine>(InstantiateAnalysisEngine);
            _pssaModulePath = Path.Combine(hostInfo.BundledModulePath, "PSScriptAnalyzer");
        }

        /// <summary>
        /// The analysis engine to use for running script analysis.
        /// </summary>
        internal PssaCmdletAnalysisEngine AnalysisEngine => _analysisEngineLazy?.Value;

        /// <summary>
        /// Sets up a script analysis run, eventually returning the result.
        /// </summary>
        /// <param name="filesToAnalyze">The files to run script analysis on.</param>
        /// <returns>A task that finishes when script diagnostics have been published.</returns>
        public void StartScriptDiagnostics(ScriptFile[] filesToAnalyze)
        {
            if (!_configurationService.CurrentSettings.ScriptAnalysis.Enable)
            {
                return;
            }

            EnsureEngineSettingsCurrent();

            if (filesToAnalyze.Length == 0)
            {
                return;
            }

            // Analyze each file independently with its own cancellation token
            foreach (ScriptFile file in filesToAnalyze)
            {
                CorrectionTableEntry fileAnalysisEntry = _mostRecentCorrectionsByFile.GetOrAdd(file, CorrectionTableEntry.CreateForFile);

                CancellationTokenSource cancellationSource = new();
                CancellationTokenSource oldTaskCancellation = Interlocked.Exchange(ref fileAnalysisEntry.CancellationSource, cancellationSource);
                if (oldTaskCancellation is not null)
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

                _ = Task.Run(() => DelayThenInvokeDiagnosticsAsync(file, fileAnalysisEntry), cancellationSource.Token);
            }
        }

        /// <summary>
        /// Formats a PowerShell script with the given settings.
        /// </summary>
        /// <param name="scriptFileContents">The script to format.</param>
        /// <param name="formatSettings">The settings to use with the formatter.</param>
        /// <param name="formatRange">Optionally, the range that should be formatted.</param>
        /// <returns>The text of the formatted PowerShell script.</returns>
        public Task<string> FormatAsync(string scriptFileContents, Hashtable formatSettings, int[] formatRange = null)
        {
            EnsureEngineSettingsCurrent();
            return AnalysisEngine.FormatAsync(scriptFileContents, formatSettings, formatRange);
        }

        /// <summary>
        /// Get comment help text for a PowerShell function definition.
        /// </summary>
        /// <param name="functionText">The text of the function to get comment help for.</param>
        /// <param name="helpLocation">A string referring to which location comment help should be placed around the function.</param>
        /// <param name="forBlockComment">If true, block comment help will be supplied.</param>
        /// <returns></returns>
        public async Task<string> GetCommentHelpText(string functionText, string helpLocation, bool forBlockComment)
        {
            if (AnalysisEngine is null)
            {
                return null;
            }

            Hashtable commentHelpSettings = GetCommentHelpRuleSettings(helpLocation, forBlockComment);

            ScriptFileMarker[] analysisResults = await AnalysisEngine.AnalyzeScriptAsync(functionText, commentHelpSettings).ConfigureAwait(false);

            if (analysisResults.Length == 0 || !analysisResults[0].Corrections.Any())
            {
                return null;
            }

            return analysisResults[0].Corrections.First().Edit.Text;
        }

        /// <summary>
        /// Get the most recent corrections computed for a given script file.
        /// </summary>
        /// <param name="uri">The URI string of the file to get code actions for.</param>
        /// <returns>A thread-safe readonly dictionary of the code actions of the particular file.</returns>
        public async Task<IReadOnlyDictionary<string, IEnumerable<MarkerCorrection>>> GetMostRecentCodeActionsForFileAsync(DocumentUri uri)
        {
            if (!_workspaceService.TryGetFile(uri, out ScriptFile file)
                || !_mostRecentCorrectionsByFile.TryGetValue(file, out CorrectionTableEntry corrections))
            {
                return null;
            }

            // Wait for diagnostics to be published for this file
            #pragma warning disable VSTHRD003
            await corrections.DiagnosticPublish.ConfigureAwait(false);
            #pragma warning restore VSTHRD003

            return corrections.Corrections;
        }

        /// <summary>
        /// Clear all diagnostic markers for a given file.
        /// </summary>
        /// <param name="file">The file to clear markers in.</param>
        /// <returns>A task that ends when all markers in the file have been cleared.</returns>
        public void ClearMarkers(ScriptFile file) => PublishScriptDiagnostics(file, new List<ScriptFileMarker>());

        /// <summary>
        /// Event subscription method to be run when PSES configuration has been updated.
        /// </summary>
        /// <param name="_">The sender of the configuration update event.</param>
        /// <param name="settings">The new language server settings.</param>
        public void OnConfigurationUpdated(object _, LanguageServerSettings settings)
        {
            if (settings.ScriptAnalysis.Enable)
            {
                InitializeAnalysisEngineToCurrentSettings();
            }
        }

        private void EnsureEngineSettingsCurrent()
        {
            if (_analysisEngineLazy is null
                    || (_pssaSettingsFilePath is not null
                        && !File.Exists(_pssaSettingsFilePath)))
            {
                InitializeAnalysisEngineToCurrentSettings();
            }
        }

        private void InitializeAnalysisEngineToCurrentSettings()
        {
            // We may be triggered after the lazy factory is set,
            // but before it's been able to instantiate
            if (_analysisEngineLazy is null)
            {
                _analysisEngineLazy = new Lazy<PssaCmdletAnalysisEngine>(InstantiateAnalysisEngine);
                return;
            }
            else if (!_analysisEngineLazy.IsValueCreated)
            {
                return;
            }

            // Retrieve the current script analysis engine so we can recreate it after we've overridden it
            PssaCmdletAnalysisEngine currentAnalysisEngine = AnalysisEngine;

            // Clear the open file markers and set the new engine factory
            ClearOpenFileMarkers();
            _analysisEngineLazy = new Lazy<PssaCmdletAnalysisEngine>(() => RecreateAnalysisEngine(currentAnalysisEngine));
        }

        internal PssaCmdletAnalysisEngine InstantiateAnalysisEngine()
        {
            PssaCmdletAnalysisEngine.Builder pssaCmdletEngineBuilder = new(_loggerFactory);

            // If there's a settings file use that
            if (TryFindSettingsFile(out string settingsFilePath))
            {
                _logger.LogInformation($"Configuring PSScriptAnalyzer with rules at '{settingsFilePath}'");
                _pssaSettingsFilePath = settingsFilePath;
                pssaCmdletEngineBuilder.WithSettingsFile(settingsFilePath);
            }
            else
            {
                _logger.LogInformation("PSScriptAnalyzer settings file not found. Falling back to default rules");
                pssaCmdletEngineBuilder.WithIncludedRules(s_defaultRules);
            }

            return pssaCmdletEngineBuilder.Build(_pssaModulePath);
        }

        private PssaCmdletAnalysisEngine RecreateAnalysisEngine(PssaCmdletAnalysisEngine oldAnalysisEngine)
        {
            if (TryFindSettingsFile(out string settingsFilePath))
            {
                _logger.LogInformation($"Recreating analysis engine with rules at '{settingsFilePath}'");
                _pssaSettingsFilePath = settingsFilePath;
                return oldAnalysisEngine.RecreateWithNewSettings(settingsFilePath);
            }

            _logger.LogInformation("PSScriptAnalyzer settings file not found. Falling back to default rules");
            return oldAnalysisEngine.RecreateWithRules(s_defaultRules);
        }

        private bool TryFindSettingsFile(out string settingsFilePath)
        {
            string configuredPath = _configurationService?.CurrentSettings.ScriptAnalysis.SettingsPath;

            if (string.IsNullOrEmpty(configuredPath))
            {
                settingsFilePath = null;
                return false;
            }

            settingsFilePath = _workspaceService?.FindFileInWorkspace(configuredPath);

            if (settingsFilePath is null
                || !File.Exists(settingsFilePath))
            {
                _logger.LogInformation($"Unable to find PSSA settings file at '{configuredPath}'. Loading default rules.");
                settingsFilePath = null;
                return false;
            }

            _logger.LogInformation($"Found PSSA settings file at '{settingsFilePath}'");

            return true;
        }

        private void ClearOpenFileMarkers()
        {
            foreach (ScriptFile file in _workspaceService.GetOpenedFiles())
            {
                ClearMarkers(file);
            }
        }

        internal async Task DelayThenInvokeDiagnosticsAsync(ScriptFile fileToAnalyze, CorrectionTableEntry fileAnalysisEntry)
        {
            CancellationToken cancellationToken = fileAnalysisEntry.CancellationSource.Token;
            Task previousAnalysisTask = fileAnalysisEntry.DiagnosticPublish;

            // Shouldn't start a new analysis task until:
            //  1. Delay/debounce period finishes (i.e. user has not started typing again)
            //  2. Previous analysis task finishes (runspace pool is capped at 1, so we'd be sitting in a queue there)
            Task debounceAndPrevious = Task.WhenAll(Task.Delay(_analysisDelayMillis), previousAnalysisTask);

            // In parallel, we will keep an eye on our cancellation token
            Task cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);

            if (cancellationTask == await Task.WhenAny(debounceAndPrevious, cancellationTask).ConfigureAwait(false))
            {
                return;
            }

            // If we've made it past the delay period then we don't care
            // about the cancellation token anymore.  This could happen
            // when the user stops typing for long enough that the delay
            // period ends but then starts typing while analysis is going
            // on.  It makes sense to send back the results from the first
            // delay period while the second one is ticking away.

            TaskCompletionSource<ScriptFileMarker[]> placeholder = new TaskCompletionSource<ScriptFileMarker[]>();

            // Try to take the place of the currently running task by atomically writing our task in the fileAnalysisEntry.
            Task valueAtExchange = Interlocked.CompareExchange(ref fileAnalysisEntry.DiagnosticPublish, placeholder.Task, previousAnalysisTask);

            if (valueAtExchange != previousAnalysisTask)
            {
                // Some other task has managed to jump in front of us i.e. fileAnalysisEntry.DiagnosticPublish is
                // no longer equal to previousAnalysisTask which we noted down at the start of this method
                _logger.LogDebug("Failed to claim the running analysis task spot");
                return;
            }

            // Successfully claimed the running task slot, we can actually run the analysis now
            try
            {
                ScriptFileMarker[] semanticMarkers = await AnalysisEngine.AnalyzeScriptAsync(fileToAnalyze.Contents).ConfigureAwait(false);
                placeholder.SetResult(semanticMarkers);

                // Clear existing PSScriptAnalyzer markers (but keep parser errors where the source is "PowerShell")
                // so that they are not duplicated when re-opening files.
                fileToAnalyze.DiagnosticMarkers.RemoveAll(m => m.Source == "PSScriptAnalyzer");
                fileToAnalyze.DiagnosticMarkers.AddRange(semanticMarkers);

                PublishScriptDiagnostics(fileToAnalyze);
            }
            catch (Exception ex)
            {
                placeholder.SetException(ex);
                throw;
            }
        }

        private void PublishScriptDiagnostics(ScriptFile scriptFile) => PublishScriptDiagnostics(scriptFile, scriptFile.DiagnosticMarkers);

        private void PublishScriptDiagnostics(ScriptFile scriptFile, List<ScriptFileMarker> markers)
        {
            // NOTE: Sometimes we have null markers for reasons we don't yet know, but we need to
            // remove them.
            _ = markers.RemoveAll(m => m is null);
            Diagnostic[] diagnostics = new Diagnostic[markers.Count];

            CorrectionTableEntry fileCorrections = _mostRecentCorrectionsByFile.GetOrAdd(
                scriptFile,
                CorrectionTableEntry.CreateForFile);

            fileCorrections.Corrections.Clear();

            for (int i = 0; i < markers.Count; i++)
            {
                ScriptFileMarker marker = markers[i];

                Diagnostic diagnostic = GetDiagnosticFromMarker(marker);

                if (marker.Corrections is not null)
                {
                    string diagnosticId = GetUniqueIdFromDiagnostic(diagnostic);
                    fileCorrections.Corrections[diagnosticId] = marker.Corrections;
                }

                diagnostics[i] = diagnostic;
            }

            _languageServer?.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = scriptFile.DocumentUri,
                Diagnostics = new Container<Diagnostic>(diagnostics)
            });
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
            return markerLevel switch
            {
                ScriptFileMarkerLevel.Error => DiagnosticSeverity.Error,
                ScriptFileMarkerLevel.Warning => DiagnosticSeverity.Warning,
                ScriptFileMarkerLevel.Information => DiagnosticSeverity.Information,
                _ => DiagnosticSeverity.Error,
            };
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
                    }}},
                { "IncludeRules", "PSProvideCommentHelp" }};
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_analysisEngineLazy?.IsValueCreated == true)
                    {
                        _analysisEngineLazy.Value.Dispose();
                    }

                    foreach (CorrectionTableEntry entry in _mostRecentCorrectionsByFile.Values)
                    {
                        entry.Dispose();
                    }
                    _mostRecentCorrectionsByFile.Clear();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() =>
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        #endregion

        /// <summary>
        /// Tracks corrections suggested by PSSA for a given file,
        /// so that after a diagnostics request has fired,
        /// a code action request can look up that file,
        /// wait for analysis to finish if needed,
        /// and then fetch the corrections set in the table entry by PSSA.
        /// </summary>
        internal class CorrectionTableEntry : IDisposable
        {
            public static CorrectionTableEntry CreateForFile(ScriptFile _) => new();

            public CorrectionTableEntry()
            {
                Corrections = new ConcurrentDictionary<string, IEnumerable<MarkerCorrection>>();
                DiagnosticPublish = Task.CompletedTask;
                CancellationSource = new CancellationTokenSource();
            }

            public ConcurrentDictionary<string, IEnumerable<MarkerCorrection>> Corrections { get; }

            public Task DiagnosticPublish;

            public CancellationTokenSource CancellationSource;

            public void Dispose() =>
                CancellationSource?.Dispose();
        }
    }
}
