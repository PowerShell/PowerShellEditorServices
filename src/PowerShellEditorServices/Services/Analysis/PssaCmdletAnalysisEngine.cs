// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.Analysis
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    /// <summary>
    /// PowerShell script analysis engine that uses PSScriptAnalyzer
    /// cmdlets run through a PowerShell API to drive analysis.
    /// </summary>
    internal class PssaCmdletAnalysisEngine : IDisposable
    {
        /// <summary>
        /// Builder for the PssaCmdletAnalysisEngine allowing settings configuration.
        /// </summary>
        public class Builder
        {
            private readonly ILoggerFactory _loggerFactory;

            private object _settingsParameter;

            private string[] _rules;

            /// <summary>
            /// Create a builder for PssaCmdletAnalysisEngine construction.
            /// </summary>
            /// <param name="loggerFactory">The logger to use.</param>
            public Builder(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

            /// <summary>
            /// Uses a settings file for PSSA rule configuration.
            /// </summary>
            /// <param name="settingsPath">The absolute path to the settings file.</param>
            /// <returns>The builder for chaining.</returns>
            public Builder WithSettingsFile(string settingsPath)
            {
                _settingsParameter = settingsPath;
                return this;
            }

            /// <summary>
            /// Uses a set of unconfigured rules for PSSA configuration.
            /// </summary>
            /// <param name="rules">The rules for PSSA to run.</param>
            /// <returns>The builder for chaining.</returns>
            public Builder WithIncludedRules(string[] rules)
            {
                _rules = rules;
                return this;
            }

            /// <summary>
            /// Attempts to build a PssaCmdletAnalysisEngine with the given configuration.
            /// If PSScriptAnalyzer cannot be found, this will return null.
            /// </summary>
            /// <returns>A newly configured PssaCmdletAnalysisEngine, or null if PSScriptAnalyzer cannot be found.</returns>
            public PssaCmdletAnalysisEngine Build(string pssaModulePath)
            {
                // RunspacePool takes care of queuing commands for us so we do not
                // need to worry about executing concurrent commands
                ILogger logger = _loggerFactory.CreateLogger<PssaCmdletAnalysisEngine>();

                logger.LogDebug("Creating PSScriptAnalyzer runspace with module at: '{Path}'", pssaModulePath);
                RunspacePool pssaRunspacePool = CreatePssaRunspacePool(pssaModulePath);
                PssaCmdletAnalysisEngine cmdletAnalysisEngine = new(logger, pssaRunspacePool, _rules, _settingsParameter);
                cmdletAnalysisEngine.LogAvailablePssaFeatures();
                return cmdletAnalysisEngine;
            }
        }

        /// <summary>
        /// The indentation to add when the logger lists errors.
        /// </summary>
        private static readonly string s_indentJoin = Environment.NewLine + "    ";

        private static readonly IReadOnlyCollection<PSObject> s_emptyDiagnosticResult = new Collection<PSObject>();

        private static readonly ScriptFileMarkerLevel[] s_scriptMarkerLevels = new[]
        {
            ScriptFileMarkerLevel.Error,
            ScriptFileMarkerLevel.Warning,
            ScriptFileMarkerLevel.Information
        };

        private readonly ILogger _logger;

        private readonly RunspacePool _analysisRunspacePool;

        internal readonly object _settingsParameter;

        internal readonly string[] _rulesToInclude;

        private PssaCmdletAnalysisEngine(
            ILogger logger,
            RunspacePool analysisRunspacePool,
            string[] rulesToInclude = default,
            object analysisSettingsParameter = default)
        {
            _logger = logger;
            _analysisRunspacePool = analysisRunspacePool;
            _rulesToInclude = rulesToInclude;
            _settingsParameter = analysisSettingsParameter;
        }

        /// <summary>
        /// Format a script given its contents.
        /// TODO: This needs to be cancellable.
        /// </summary>
        /// <param name="scriptDefinition">The full text of a script.</param>
        /// <param name="formatSettings">The formatter settings to use.</param>
        /// <param name="rangeList">A possible range over which to run the formatter.</param>
        /// <returns>Formatted script as string</returns>
        public async Task<string> FormatAsync(string scriptDefinition, Hashtable formatSettings, int[] rangeList)
        {
            // We cannot use Range type therefore this workaround of using -1 default value.
            // Invoke-Formatter throws a ParameterBinderValidationException if the ScriptDefinition is an empty string.
            if (string.IsNullOrEmpty(scriptDefinition))
            {
                _logger.LogDebug("Script Definition was: " + scriptDefinition is null ? "null" : "empty string");
                return scriptDefinition;
            }

            PSCommand psCommand = new PSCommand()
                .AddCommand("Invoke-Formatter")
                .AddParameter("ScriptDefinition", scriptDefinition)
                .AddParameter("Settings", formatSettings);

            if (rangeList is not null)
            {
                psCommand.AddParameter("Range", rangeList);
            }

            PowerShellResult result = await InvokePowerShellAsync(psCommand).ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogError("Formatter returned null result");
                return null;
            }

            if (result.HasErrors)
            {
                StringBuilder errorBuilder = new StringBuilder().Append(s_indentJoin);
                foreach (ErrorRecord err in result.Errors)
                {
                    errorBuilder.Append(err).Append(s_indentJoin);
                }
                _logger.LogWarning($"Errors found while formatting file: {errorBuilder}");
                return null;
            }

            foreach (PSObject resultObj in result.Output)
            {
                if (resultObj?.BaseObject is string formatResult)
                {
                    return formatResult;
                }
            }

            _logger.LogError("Couldn't get result from output. Returning null.");
            return null;
        }

        /// <summary>
        /// Analyze a given script using PSScriptAnalyzer.
        /// </summary>
        /// <param name="scriptContent">The contents of the script to analyze.</param>
        /// <returns>An array of markers indicating script analysis diagnostics.</returns>
        public Task<ScriptFileMarker[]> AnalyzeScriptAsync(string scriptContent) => AnalyzeScriptAsync(scriptContent, settings: null);

        /// <summary>
        /// Analyze a given script using PSScriptAnalyzer.
        /// </summary>
        /// <param name="scriptContent">The contents of the script to analyze.</param>
        /// <param name="settings">The settings file to use in this instance of analysis.</param>
        /// <returns>An array of markers indicating script analysis diagnostics.</returns>
        public Task<ScriptFileMarker[]> AnalyzeScriptAsync(string scriptContent, Hashtable settings)
        {
            // When a new, empty file is created there are by definition no issues.
            // Furthermore, if you call Invoke-ScriptAnalyzer with an empty ScriptDefinition
            // it will generate a ParameterBindingValidationException.
            if (string.IsNullOrEmpty(scriptContent))
            {
                return Task.FromResult(Array.Empty<ScriptFileMarker>());
            }

            PSCommand command = new PSCommand()
                .AddCommand("Invoke-ScriptAnalyzer")
                .AddParameter("ScriptDefinition", scriptContent)
                .AddParameter("Severity", s_scriptMarkerLevels);

            object settingsValue = settings ?? _settingsParameter;
            if (settingsValue is not null)
            {
                command.AddParameter("Settings", settingsValue);
            }
            else
            {
                command.AddParameter("IncludeRule", _rulesToInclude);
            }

            return GetSemanticMarkersFromCommandAsync(command);
        }

        public PssaCmdletAnalysisEngine RecreateWithNewSettings(string settingsPath) => new(
            _logger,
            _analysisRunspacePool,
            rulesToInclude: null,
            analysisSettingsParameter: settingsPath);

        public PssaCmdletAnalysisEngine RecreateWithRules(string[] rules) => new(
            _logger,
            _analysisRunspacePool,
            rulesToInclude: rules,
            analysisSettingsParameter: null);

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _analysisRunspacePool.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() =>
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

        #endregion

        private async Task<ScriptFileMarker[]> GetSemanticMarkersFromCommandAsync(PSCommand command)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            PowerShellResult result = await InvokePowerShellAsync(command).ConfigureAwait(false);
            stopwatch.Stop();

            IReadOnlyCollection<PSObject> diagnosticResults = result?.Output ?? s_emptyDiagnosticResult;
            _logger.LogDebug(string.Format("Found {0} violations in {1}ms", diagnosticResults.Count, stopwatch.ElapsedMilliseconds));

            ScriptFileMarker[] scriptMarkers = new ScriptFileMarker[diagnosticResults.Count];
            int i = 0;
            foreach (PSObject diagnostic in diagnosticResults)
            {
                scriptMarkers[i] = ScriptFileMarker.FromDiagnosticRecord(diagnostic);
                i++;
            }

            return scriptMarkers;
        }

        // TODO: Deduplicate this logic and cleanup using lessons learned from pipeline rewrite.
        private Task<PowerShellResult> InvokePowerShellAsync(PSCommand command) => Task.Run(() => InvokePowerShell(command));

        private PowerShellResult InvokePowerShell(PSCommand command)
        {
            using PowerShell pwsh = PowerShell.Create(RunspaceMode.NewRunspace);
            pwsh.RunspacePool = _analysisRunspacePool;
            pwsh.Commands = command;
            PowerShellResult result = null;
            try
            {
                Collection<PSObject> output = pwsh.Invoke();
                PSDataCollection<ErrorRecord> errors = pwsh.Streams.Error;
                result = new PowerShellResult(output, errors, pwsh.HadErrors);
            }
            catch (CommandNotFoundException ex)
            {
                // This exception is possible if the module path loaded
                // is wrong even though PSScriptAnalyzer is available as a module
                _logger.LogError(ex.Message);
            }
            catch (CmdletInvocationException ex)
            {
                // We do not want to crash EditorServices for exceptions caused by cmdlet invocation.
                // The main reasons that cause the exception are:
                // * PSCmdlet.WriteOutput being called from another thread than Begin/Process
                // * CompositionContainer.ComposeParts complaining that "...Only one batch can be composed at a time"
                // * PSScriptAnalyzer not being able to find its PSScriptAnalyzer.psd1 because we are hosted by an Assembly other than pwsh.exe
                string message = ex.Message;
                if (!string.IsNullOrEmpty(ex.ErrorRecord.FullyQualifiedErrorId))
                {
                    // Microsoft.PowerShell.EditorServices.Services.Analysis.PssaCmdletAnalysisEngine: Exception of type 'System.Exception' was thrown. |
                    message += $" | {ex.ErrorRecord.FullyQualifiedErrorId}";
                }
                _logger.LogError(message);
            }

            return result;
        }

        /// <summary>
        /// Log the features available from the PSScriptAnalyzer module that has been imported
        /// for use with the AnalysisService.
        /// </summary>
        private void LogAvailablePssaFeatures()
        {
            // Save ourselves some work here
            if (!_logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine("PSScriptAnalyzer successfully imported:").AppendLine("    Available Rules:");

            // Log available rules
            foreach (string ruleName in GetPSScriptAnalyzerRules())
            {
                sb.Append("        ").AppendLine(ruleName);
            }

            _logger.LogDebug(sb.ToString());
        }

        /// <summary>
        /// Returns a list of builtin-in PSScriptAnalyzer rules
        /// </summary>
        private IEnumerable<string> GetPSScriptAnalyzerRules()
        {
            PowerShellResult getRuleResult = InvokePowerShell(new PSCommand().AddCommand("Get-ScriptAnalyzerRule"));
            if (getRuleResult is null)
            {
                _logger.LogWarning("Get-ScriptAnalyzerRule returned null result");
                return Enumerable.Empty<string>();
            }

            List<string> ruleNames = new(getRuleResult.Output.Count);
            foreach (PSObject rule in getRuleResult.Output)
            {
                ruleNames.Add((string)rule.Members["RuleName"].Value);
            }

            return ruleNames;
        }

        /// <summary>
        /// Create a new runspace pool around a PSScriptAnalyzer module for asynchronous script analysis tasks.
        /// This looks for the latest version of PSScriptAnalyzer on the path and loads that.
        /// </summary>
        /// <returns>A runspace pool with PSScriptAnalyzer loaded for running script analysis tasks.</returns>
        private static RunspacePool CreatePssaRunspacePool(string pssaModulePath)
        {
            using PowerShell pwsh = PowerShell.Create(RunspaceMode.NewRunspace);

            // Now that we know where the PSScriptAnalyzer we want to use is, create a base
            // session state with PSScriptAnalyzer loaded
            //
            // We intentionally use `CreateDefault2()` as it loads `Microsoft.PowerShell.Core`
            // only, which is a more minimal and therefore safer state.
            InitialSessionState sessionState = InitialSessionState.CreateDefault2();

            // We set the runspace's execution policy `Bypass` so we can always import our bundled
            // PSScriptAnalyzer module.
            if (VersionUtils.IsWindows)
            {
                sessionState.ExecutionPolicy = ExecutionPolicy.Bypass;
            }

            sessionState.ImportPSModulesFromPath(pssaModulePath);

            RunspacePool runspacePool = RunspaceFactory.CreateRunspacePool(sessionState);

            runspacePool.SetMaxRunspaces(1);
            runspacePool.ThreadOptions = PSThreadOptions.ReuseThread;
            runspacePool.Open();

            return runspacePool;
        }

        /// <summary>
        /// Wraps the result of an execution of PowerShell to send back through
        /// asynchronous calls.
        /// </summary>
        private class PowerShellResult
        {
            public PowerShellResult(
                Collection<PSObject> output,
                PSDataCollection<ErrorRecord> errors,
                bool hasErrors)
            {
                Output = output;
                Errors = errors;
                HasErrors = hasErrors;
            }

            public Collection<PSObject> Output { get; }

            public PSDataCollection<ErrorRecord> Errors { get; }

            public bool HasErrors { get; }
        }
    }
}
