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
using Microsoft.Windows.PowerShell.ScriptAnalyzer.Hosting;
using Microsoft.Windows.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.PowerShell.EditorServices.Services.Analysis;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Provides a high-level service for performing semantic analysis
    /// of PowerShell scripts.
    /// </summary>
    public class AnalysisService : IDisposable
    {
        #region Fields

        /// <summary>
        /// Defines the list of Script Analyzer rules to include by default if
        /// no settings file is specified.
        /// </summary>
        private static readonly string[] s_includedRules = {
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
        private readonly HostedAnalyzer _analyzer;
        private readonly Settings _analyzerSettings;
        private readonly ILanguageServer _languageServer;
        private readonly ConfigurationService _configurationService;
        private readonly ConcurrentDictionary<string, (SemaphoreSlim, Dictionary<string, MarkerCorrection>)> _mostRecentCorrectionsByFile;

        private CancellationTokenSource _existingRequestCancellation;
        private readonly SemaphoreSlim _existingRequestCancellationLock;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the path to a settings file (.psd1)
        /// containing PSScriptAnalyzer settings.
        /// </summary>
        public string SettingsPath { get; internal set; }

        #endregion

        #region Constructors

        public AnalysisService(ConfigurationService configurationService, ILanguageServer languageServer, ILoggerFactory factory)
        {
            SettingsPath = configurationService.CurrentSettings.ScriptAnalysis.SettingsPath;
            _logger = factory.CreateLogger<AnalysisService>();
            _analyzer = new HostedAnalyzer();
            _analyzerSettings = _analyzer.CreateSettings(s_includedRules);
            _analyzerSettings.Severities.AddRange(new [] {
                RuleSeverity.Error.ToString(),
                RuleSeverity.Information.ToString(),
                RuleSeverity.Information.ToString()
            });
            _configurationService = configurationService;
            _languageServer = languageServer;
            _mostRecentCorrectionsByFile = new ConcurrentDictionary<string, (SemaphoreSlim, Dictionary<string, MarkerCorrection>)>();
            _existingRequestCancellation = new CancellationTokenSource();
            _existingRequestCancellationLock = AsyncUtils.CreateSimpleLockingSemaphore();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Clean up resources.
        /// </summary>
        public void Dispose()
        {
            _existingRequestCancellation.Dispose();
            _analyzer.Dispose();
            _existingRequestCancellationLock.Dispose();
        }

        /// <summary>
        /// Get PSScriptAnalyzer settings for PSProvideCommentHelp rule.
        /// </summary>
        /// <param name="enable">Enable the rule.</param>
        /// <param name="exportedOnly">Analyze only exported functions/cmdlets.</param>
        /// <param name="blockComment">Use block comment or line comment.</param>
        /// <param name="vscodeSnippetCorrection">Return a vscode snipped correction should be returned.</param>
        /// <param name="placement">Place comment help at the given location relative to the function definition.</param>
        /// <returns>A PSScriptAnalyzer settings.</returns>
        public Settings GetCommentHelpRuleSettings(
            bool enable,
            bool exportedOnly,
            bool blockComment,
            bool vscodeSnippetCorrection,
            string placement)
        {
            var pssaSettings = _analyzer.CreateSettings();
            pssaSettings.AddRuleArgument("PSProvideCommentHelp", new Dictionary<string, object>{
                { "Enable", enable },
                { "ExportedOnly", exportedOnly },
                { "BlockComment", blockComment },
                { "VSCodeSnippetCorrection", vscodeSnippetCorrection },
                { "Placement", placement }
            });

            return pssaSettings;
        }

        /// <summary>
        /// Perform semantic analysis on the given script with the given settings.
        /// </summary>
        /// <param name="scriptContent">The script content to be analyzed.</param>
        /// <param name="settings">ScriptAnalyzer settings</param>
        /// <returns></returns>
        public async Task<List<Diagnostic>> GetSemanticMarkersAsync(
           string scriptContent,
           Settings settings)
        {
            AnalyzerResult analyzerResult = await _analyzer.AnalyzeAsync(
                    scriptContent,
                    settings);

            return analyzerResult.Result.Select(DiagnosticCreationHelper.FromDiagnosticRecord).ToList();
        }

        /// <summary>
        /// Format a given script text with default codeformatting settings.
        /// </summary>
        /// <param name="scriptDefinition">Script text to be formatted</param>
        /// <param name="settings">ScriptAnalyzer settings</param>
        /// <param name="rangeList">The range within which formatting should be applied.</param>
        /// <returns>The formatted script text.</returns>
        public async Task<string> FormatAsync(
            string scriptDefinition,
            int tabSize,
            bool insertSpaces,
            Windows.PowerShell.ScriptAnalyzer.Range range)
        {
            // We cannot use Range type therefore this workaround of using -1 default value.
            // Invoke-Formatter throws a ParameterBinderValidationException if the ScriptDefinition is an empty string.
            if (string.IsNullOrEmpty(scriptDefinition))
            {
                return null;
            }

            Settings settings = _configurationService.CurrentSettings.CodeFormatting.GetFormatterSettings(
                _analyzer,
                tabSize,
                insertSpaces);

            return range == null
                ? await _analyzer.FormatAsync(_analyzer.Fix(scriptDefinition, settings), settings)
                : await _analyzer.FormatAsync(_analyzer.Fix(scriptDefinition, range, settings), settings, range);
        }

        public async Task RunScriptDiagnosticsAsync(
            ScriptFile[] filesToAnalyze,
            CancellationToken token)
        {
            // This token will be cancelled (typically via LSP cancellation) if the token passed in is cancelled or if
            // multiple requests come in (the last one wins).
            CancellationToken ct;

            // If there's an existing task, attempt to cancel it
            try
            {
                await _existingRequestCancellationLock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
                // Try to cancel the request
                _existingRequestCancellation.Cancel();

                // If cancellation didn't throw an exception,
                // clean up the existing token
                _existingRequestCancellation.Dispose();
                _existingRequestCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                ct = _existingRequestCancellation.Token;
            }
            catch (Exception e)
            {
                // TODO: Catch a more specific exception!
                _logger.LogError(
                    string.Format(
                        "Exception while canceling analysis task:\n\n{0}",
                        e.ToString()));

                TaskCompletionSource<bool> cancelTask = new TaskCompletionSource<bool>();
                cancelTask.SetCanceled();
                return;
            }
            finally
            {
                _existingRequestCancellationLock.Release();
            }

            // If filesToAnalyze is empty, nothing to do so return early.
            if (filesToAnalyze.Length == 0)
            {
                return;
            }

            try
            {
                // Wait for the desired delay period before analyzing the provided list of files.
                await Task.Delay(750, ct).ConfigureAwait(continueOnCapturedContext: false);

                foreach (ScriptFile file in filesToAnalyze)
                {
                    if (!ct.CanBeCanceled || ct.IsCancellationRequested)
                    {
                        break;
                    }

                    AnalyzerResult analyzerResult = await _analyzer.AnalyzeAsync(
                        file.ScriptAst,
                        file.ScriptTokens,
                        _analyzerSettings,
                        file.FilePath);

                    if (!ct.CanBeCanceled || ct.IsCancellationRequested)
                    {
                        break;
                    }

                    // Create the entry for this file if it does not already exist
                    SemaphoreSlim fileLock;
                    Dictionary<string, MarkerCorrection> fileCorrections;
                    bool newEntryNeeded = false;
                    if (_mostRecentCorrectionsByFile.TryGetValue(file.DocumentUri, out (SemaphoreSlim, Dictionary<string, MarkerCorrection>) fileCorrectionsEntry))
                    {
                        fileLock = fileCorrectionsEntry.Item1;
                        fileCorrections = fileCorrectionsEntry.Item2;
                    }
                    else
                    {
                        fileLock = AsyncUtils.CreateSimpleLockingSemaphore();
                        fileCorrections = new Dictionary<string, MarkerCorrection>();
                        newEntryNeeded = true;
                    }

                    await fileLock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
                    try
                    {
                        if (newEntryNeeded)
                        {
                            // If we create a new entry, we should do it after acquiring the lock we just created
                            // to ensure a competing thread can never acquire it first and read invalid information from it
                            _mostRecentCorrectionsByFile[file.DocumentUri] = (fileLock, fileCorrections);
                        }
                        else
                        {
                            // Otherwise we need to clear the stale corrections
                            fileCorrections.Clear();
                        }

                        foreach (DiagnosticRecord diagnosticRecord in analyzerResult.Result)
                        {
                            var diagnostic = DiagnosticCreationHelper.FromDiagnosticRecord(diagnosticRecord);
                            file.DiagnosticMarkers.Add(diagnostic);

                            // Does the marker contain a correction?
                            if (diagnosticRecord.SuggestedCorrections != null)
                            {
                                var editRegions = new List<ScriptRegion>();
                                string correctionMessage = null;
                                foreach (dynamic suggestedCorrection in diagnosticRecord.SuggestedCorrections)
                                {
                                    editRegions.Add(
                                        new ScriptRegion(
                                            diagnosticRecord.ScriptPath,
                                            suggestedCorrection.Text,
                                            startLineNumber: suggestedCorrection.StartLineNumber,
                                            startColumnNumber: suggestedCorrection.StartColumnNumber,
                                            endLineNumber: suggestedCorrection.EndLineNumber,
                                            endColumnNumber: suggestedCorrection.EndColumnNumber,
                                            startOffset: -1,
                                            endOffset: -1));

                                    correctionMessage = suggestedCorrection.Description;
                                }

                                string diagnosticId = GetUniqueIdFromDiagnostic(diagnostic);
                                fileCorrections[diagnosticId] = new MarkerCorrection
                                {
                                    Name = correctionMessage == null ? diagnosticRecord.Message : correctionMessage,
                                    Edits = editRegions.ToArray()
                                };
                            }
                        }
                    }
                    finally
                    {
                        fileLock.Release();
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // If a cancellation was requested, then publish what we have.
            }

            foreach (var file in filesToAnalyze)
            {
                PublishScriptDiagnostics(
                    file,
                    file.DiagnosticMarkers);
            }
        }

        // send empty diagnostic markers to clear any markers associated with the given file.
        public void ClearMarkers(ScriptFile scriptFile) => PublishScriptDiagnostics(scriptFile, new List<Diagnostic>());

        public async Task<IReadOnlyDictionary<string, MarkerCorrection>> GetMostRecentCodeActionsForFileAsync(string documentUri)
        {
            if (!_mostRecentCorrectionsByFile.TryGetValue(documentUri, out (SemaphoreSlim fileLock, Dictionary<string, MarkerCorrection> corrections) fileCorrectionsEntry))
            {
                return null;
            }

            await fileCorrectionsEntry.fileLock.WaitAsync();
            // We must copy the dictionary for thread safety
            var corrections = new Dictionary<string, MarkerCorrection>(fileCorrectionsEntry.corrections.Count);
            try
            {
                foreach (KeyValuePair<string, MarkerCorrection> correction in fileCorrectionsEntry.corrections)
                {
                    corrections.Add(correction.Key, correction.Value);
                }

                return corrections;
            }
            finally
            {
                fileCorrectionsEntry.fileLock.Release();
            }
        }

        internal static string GetUniqueIdFromDiagnostic(Diagnostic diagnostic)
        {
            OmniSharp.Extensions.LanguageServer.Protocol.Models.Position start = diagnostic.Range.Start;
            OmniSharp.Extensions.LanguageServer.Protocol.Models.Position end = diagnostic.Range.End;

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
        /// Uses the PSScriptAnalyzer rule 'PSProvideCommentHelp' to get the comment-based help for a function string passed in.
        /// </summary>
        /// <param name="funcText">The string representation of the function we will get help for.</param>
        /// <param name="blockComment">Use block comment or line comment.</param>
        /// <param name="placement">Place comment help at the given location relative to the function definition.</param>
        /// <returns>A PSScriptAnalyzer settings.</returns>
        internal async Task<string> GetCommentHelpCorrectionTextAsync(string funcText, bool blockComment, string placement)
        {
            Settings commentHelpSettings = GetCommentHelpRuleSettings(
                enable: true,
                exportedOnly: false,
                blockComment: blockComment,
                vscodeSnippetCorrection: true,
                placement: placement);

            AnalyzerResult analyzerResult = await _analyzer.AnalyzeAsync(funcText, commentHelpSettings);
            return analyzerResult.Result
                .Single(record => record.RuleName == "PSProvideCommentHelp")
                .SuggestedCorrections.Single().Text;
        }

        #endregion

        private void PublishScriptDiagnostics(
            ScriptFile scriptFile,
            List<Diagnostic> diagnostics)
        {
            // Always send syntax and semantic errors.  We want to
            // make sure no out-of-date markers are being displayed.
            _languageServer.Document.PublishDiagnostics(new PublishDiagnosticsParams()
            {
                Uri = new Uri(scriptFile.DocumentUri),
                Diagnostics = new Container<Diagnostic>(diagnostics),
            });
        }
    }
}
