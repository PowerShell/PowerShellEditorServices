//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.ObjectModel;
using Microsoft.PowerShell.EditorServices.Services.Analysis;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Provides a high-level service for performing semantic analysis
    /// of PowerShell scripts.
    /// </summary>
    internal class AnalysisService : IDisposable
    {
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


        /// <summary>
        /// Factory method for producing AnalysisService instances. Handles loading of the PSScriptAnalyzer module
        /// and runspace pool instantiation before creating the service instance.
        /// </summary>
        /// <param name="logger">EditorServices logger for logging information.</param>
        /// <param name="languageServer">The language server instance to use for messaging.</param>
        /// <returns>
        /// A new analysis service instance with a freshly imported PSScriptAnalyzer module and runspace pool.
        /// Returns null if problems occur. This method should never throw.
        /// </returns>
        public static AnalysisService Create(ILogger logger, ILanguageServer languageServer)
        {
            IAnalysisEngine analysisEngine = PssaCmdletAnalysisEngine.Create(logger);

            // ?? doesn't work above sadly
            if (analysisEngine == null)
            {
                analysisEngine = new NullAnalysisEngine();
            }

            var analysisService = new AnalysisService(logger, languageServer, analysisEngine)
            {
                EnabledRules = s_defaultRules,
            };

            return analysisService;
        }

        private readonly ILogger _logger;

        private readonly ILanguageServer _languageServer;

        private readonly IAnalysisEngine _analysisEngine;

        private readonly int _analysisDelayMillis;

        private readonly ConcurrentDictionary<string, (SemaphoreSlim, Dictionary<string, MarkerCorrection>)> _mostRecentCorrectionsByFile;

        private CancellationTokenSource _diagnosticsCancellationTokenSource;

        public AnalysisService(ILogger logger, ILanguageServer languageServer, IAnalysisEngine analysisEngine)
        {
            _logger = logger;
            _languageServer = languageServer;
            _analysisEngine = analysisEngine;
            _analysisDelayMillis = 750;
            _mostRecentCorrectionsByFile = new ConcurrentDictionary<string, (SemaphoreSlim, Dictionary<string, MarkerCorrection>)>();
        }

        public string[] EnabledRules { get; set; }

        public string SettingsPath { get; set; }

        public Task RunScriptDiagnosticsAsync(
            ScriptFile[] filesToAnalyze,
            CancellationToken cancellationToken)
        {
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
                ScriptFileMarker[] semanticMarkers = SettingsPath != null
                    ? await _analysisEngine.AnalyzeScriptAsync(scriptFile.Contents, SettingsPath).ConfigureAwait(false)
                    : await _analysisEngine.AnalyzeScriptAsync(scriptFile.Contents, EnabledRules).ConfigureAwait(false);

                scriptFile.DiagnosticMarkers.AddRange(semanticMarkers);

                await PublishScriptDiagnosticsAsync(scriptFile).ConfigureAwait(false);
            }
        }

        private async Task PublishScriptDiagnosticsAsync(ScriptFile scriptFile)
        {
            (SemaphoreSlim fileLock, Dictionary<string, MarkerCorrection> fileCorrections) = _mostRecentCorrectionsByFile.GetOrAdd(
                scriptFile.DocumentUri,
                CreateFileCorrectionsEntry);

            var diagnostics = new Diagnostic[scriptFile.DiagnosticMarkers.Count];

            await fileLock.WaitAsync();
            try
            {
                fileCorrections.Clear();

                for (int i = 0; i < scriptFile.DiagnosticMarkers.Count; i++)
                {
                    ScriptFileMarker marker = scriptFile.DiagnosticMarkers[i];

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

        private static (SemaphoreSlim, Dictionary<string, MarkerCorrection>) CreateFileCorrectionsEntry(string fileUri)
        {
            return (AsyncUtils.CreateSimpleLockingSemaphore(), new Dictionary<string, MarkerCorrection>());
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

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _analysisEngine.Dispose();
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
