//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.Analysis
{
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
            /// <param name="logger">The logger to use.</param>
            public Builder(ILoggerFactory loggerFactory)
            {
                _loggerFactory = loggerFactory;
            }

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
            /// Uses a settings hashtable for PSSA rule configuration.
            /// </summary>
            /// <param name="settings">The settings hashtable to pass to PSSA.</param>
            /// <returns>The builder for chaining.</returns>
            public Builder WithSettings(Hashtable settings)
            {
                _settingsParameter = settings;
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
            public PssaCmdletAnalysisEngine Build()
            {
                // RunspacePool takes care of queuing commands for us so we do not
                // need to worry about executing concurrent commands
                ILogger logger = _loggerFactory.CreateLogger<PssaCmdletAnalysisEngine>();
                try
                {
                    RunspacePool pssaRunspacePool = CreatePssaRunspacePool(out PSModuleInfo pssaModuleInfo);

                    PssaCmdletAnalysisEngine cmdletAnalysisEngine = _settingsParameter != null
                        ? new PssaCmdletAnalysisEngine(logger, pssaRunspacePool, pssaModuleInfo, _settingsParameter)
                        : new PssaCmdletAnalysisEngine(logger, pssaRunspacePool, pssaModuleInfo, _rules);

                    cmdletAnalysisEngine.LogAvailablePssaFeatures();
                    return cmdletAnalysisEngine;
                }
                catch (FileNotFoundException e)
                {
                    logger.LogError(e, $"Unable to find PSScriptAnalyzer. Disabling script analysis. PSModulePath: '{Environment.GetEnvironmentVariable("PSModulePath")}'");
                    return null;
                }
            }
        }

        private const string PSSA_MODULE_NAME = "PSScriptAnalyzer";

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

        private readonly PSModuleInfo _pssaModuleInfo;

        private readonly object _settingsParameter;

        private readonly string[] _rulesToInclude;

        private PssaCmdletAnalysisEngine(
            ILogger logger,
            RunspacePool analysisRunspacePool,
            PSModuleInfo pssaModuleInfo,
            string[] rulesToInclude)
            : this(logger, analysisRunspacePool, pssaModuleInfo)
        {
            _rulesToInclude = rulesToInclude;
        }

        private PssaCmdletAnalysisEngine(
            ILogger logger,
            RunspacePool analysisRunspacePool,
            PSModuleInfo pssaModuleInfo,
            object analysisSettingsParameter)
            : this(logger, analysisRunspacePool, pssaModuleInfo)
        {
            _settingsParameter = analysisSettingsParameter;
        }

        private PssaCmdletAnalysisEngine(
            ILogger logger,
            RunspacePool analysisRunspacePool,
            PSModuleInfo pssaModuleInfo)
        {
            _logger = logger;
            _analysisRunspacePool = analysisRunspacePool;
            _pssaModuleInfo = pssaModuleInfo;
        }

        /// <summary>
        /// Format a script given its contents.
        /// </summary>
        /// <param name="scriptDefinition">The full text of a script.</param>
        /// <param name="formatSettings">The formatter settings to use.</param>
        /// <param name="rangeList">A possible range over which to run the formatter.</param>
        /// <returns></returns>
        public async Task<string> FormatAsync(string scriptDefinition, Hashtable formatSettings, int[] rangeList)
        {
            // We cannot use Range type therefore this workaround of using -1 default value.
            // Invoke-Formatter throws a ParameterBinderValidationException if the ScriptDefinition is an empty string.
            if (string.IsNullOrEmpty(scriptDefinition))
            {
                _logger.LogDebug("Script Definition was: " + scriptDefinition == null ? "null" : "empty string");
                return scriptDefinition;
            }

            var psCommand = new PSCommand()
                .AddCommand("Invoke-Formatter")
                .AddParameter("ScriptDefinition", scriptDefinition)
                .AddParameter("Settings", formatSettings);

            if (rangeList != null)
            {
                psCommand.AddParameter("Range", rangeList);
            }

            PowerShellResult result = await InvokePowerShellAsync(psCommand).ConfigureAwait(false);

            if (result == null)
            {
                _logger.LogError("Formatter returned null result");
                return scriptDefinition;
            }

            if (result.HasErrors)
            {
                var errorBuilder = new StringBuilder().Append(s_indentJoin);
                foreach (ErrorRecord err in result.Errors)
                {
                    errorBuilder.Append(err).Append(s_indentJoin);
                }
                _logger.LogWarning($"Errors found while formatting file: {errorBuilder}");
                return scriptDefinition;
            }

            foreach (PSObject resultObj in result.Output)
            {
                if (resultObj?.BaseObject is string formatResult)
                {
                    return formatResult;
                }
            }

            _logger.LogError("Couldn't get result from output. Returning original script.");
            return scriptDefinition;
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

            var command = new PSCommand()
                .AddCommand("Invoke-ScriptAnalyzer")
                .AddParameter("ScriptDefinition", scriptContent)
                .AddParameter("Severity", s_scriptMarkerLevels);

            object settingsValue = settings ?? _settingsParameter;
            if (settingsValue != null)
            {
                command.AddParameter("Settings", settingsValue);
            }
            else
            {
                command.AddParameter("IncludeRule", _rulesToInclude);
            }

            return GetSemanticMarkersFromCommandAsync(command);
        }

        public PssaCmdletAnalysisEngine RecreateWithNewSettings(string settingsPath)
        {
            return new PssaCmdletAnalysisEngine(_logger, _analysisRunspacePool, _pssaModuleInfo, settingsPath);
        }

        public PssaCmdletAnalysisEngine RecreateWithNewSettings(Hashtable settingsHashtable)
        {
            return new PssaCmdletAnalysisEngine(_logger, _analysisRunspacePool, _pssaModuleInfo, settingsHashtable);
        }

        public PssaCmdletAnalysisEngine RecreateWithRules(string[] rules)
        {
            return new PssaCmdletAnalysisEngine(_logger, _analysisRunspacePool, _pssaModuleInfo, rules);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

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
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion

        private async Task<ScriptFileMarker[]> GetSemanticMarkersFromCommandAsync(PSCommand command)
        {
            PowerShellResult result = await InvokePowerShellAsync(command).ConfigureAwait(false);

            IReadOnlyCollection<PSObject> diagnosticResults = result?.Output ?? s_emptyDiagnosticResult;
            _logger.LogDebug(String.Format("Found {0} violations", diagnosticResults.Count));

            var scriptMarkers = new ScriptFileMarker[diagnosticResults.Count];
            int i = 0;
            foreach (PSObject diagnostic in diagnosticResults)
            {
                scriptMarkers[i] = ScriptFileMarker.FromDiagnosticRecord(diagnostic);
                i++;
            }

            return scriptMarkers;
        }

        private Task<PowerShellResult> InvokePowerShellAsync(PSCommand command)
        {
            return Task.Run(() => InvokePowerShell(command));
        }

        private PowerShellResult InvokePowerShell(PSCommand command)
        {
            using (var powerShell = System.Management.Automation.PowerShell.Create())
            {
                powerShell.RunspacePool = _analysisRunspacePool;
                powerShell.Commands = command;
                PowerShellResult result = null;
                try
                {
                    Collection<PSObject> output = InvokePowerShellWithModulePathPreservation(powerShell);
                    PSDataCollection<ErrorRecord> errors = powerShell.Streams.Error;
                    result = new PowerShellResult(output, errors, powerShell.HadErrors);
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
                    // Two main reasons that cause the exception are:
                    // * PSCmdlet.WriteOutput being called from another thread than Begin/Process
                    // * CompositionContainer.ComposeParts complaining that "...Only one batch can be composed at a time"
                    _logger.LogError(ex.Message);
                }

                return result;
            }
        }

        /// <summary>
        /// Execute PSScriptAnalyzer cmdlets in PowerShell while preserving the PSModulePath.
        /// Attempts to prevent PSModulePath mutation by runspace creation within the PSScriptAnalyzer module.
        /// </summary>
        /// <param name="powershell">The PowerShell instance to execute.</param>
        /// <returns>The output of PowerShell execution.</returns>
        private Collection<PSObject> InvokePowerShellWithModulePathPreservation(System.Management.Automation.PowerShell powershell)
        {
            using (PSModulePathPreserver.Take())
            {
                return powershell.Invoke();
            }
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

            if (_pssaModuleInfo == null)
            {
                throw new FileNotFoundException("Unable to find loaded PSScriptAnalyzer module for logging");
            }

            var sb = new StringBuilder();
            sb.AppendLine("PSScriptAnalyzer successfully imported:");

            // Log version
            sb.Append("    Version: ");
            sb.AppendLine(_pssaModuleInfo.Version.ToString());

            // Log exported cmdlets
            sb.AppendLine("    Exported Cmdlets:");
            foreach (string cmdletName in _pssaModuleInfo.ExportedCmdlets.Keys.OrderBy(name => name))
            {
                sb.Append("    ");
                sb.AppendLine(cmdletName);
            }

            // Log available rules
            sb.AppendLine("    Available Rules:");
            foreach (string ruleName in GetPSScriptAnalyzerRules())
            {
                sb.Append("        ");
                sb.AppendLine(ruleName);
            }

            _logger.LogDebug(sb.ToString());
        }

        /// <summary>
        /// Returns a list of builtin-in PSScriptAnalyzer rules
        /// </summary>
        private IEnumerable<string> GetPSScriptAnalyzerRules()
        {
            PowerShellResult getRuleResult = InvokePowerShell(new PSCommand().AddCommand("Get-ScriptAnalyzerRule"));
            if (getRuleResult == null)
            {
                _logger.LogWarning("Get-ScriptAnalyzerRule returned null result");
                return Enumerable.Empty<string>();
            }

            var ruleNames = new List<string>(getRuleResult.Output.Count);
            foreach (var rule in getRuleResult.Output)
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
        private static RunspacePool CreatePssaRunspacePool(out PSModuleInfo pssaModuleInfo)
        {
            using (var ps = System.Management.Automation.PowerShell.Create())
            {
                // Run `Get-Module -ListAvailable -Name "PSScriptAnalyzer"`
                ps.AddCommand("Get-Module")
                    .AddParameter("ListAvailable")
                    .AddParameter("Name", PSSA_MODULE_NAME);

                try
                {
                    using (PSModulePathPreserver.Take())
                    {
                        // Get the latest version of PSScriptAnalyzer we can find
                        pssaModuleInfo = ps.Invoke<PSModuleInfo>()?
                            .OrderByDescending(moduleInfo => moduleInfo.Version)
                            .FirstOrDefault();
                    }
                }
                catch (Exception e)
                {
                    throw new FileNotFoundException("Unable to find PSScriptAnalyzer module on the module path", e);
                }

                if (pssaModuleInfo == null)
                {
                    throw new FileNotFoundException("Unable to find PSScriptAnalyzer module on the module path");
                }

                // Now that we know where the PSScriptAnalyzer we want to use is,
                // create a base session state with PSScriptAnalyzer loaded
#if DEBUG
                InitialSessionState sessionState = Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1"
                    ? InitialSessionState.CreateDefault()
                    : InitialSessionState.CreateDefault2();
#else
                InitialSessionState sessionState = InitialSessionState.CreateDefault2();
#endif

                sessionState.ImportPSModule(new [] { pssaModuleInfo.ModuleBase });

                RunspacePool runspacePool = RunspaceFactory.CreateRunspacePool(sessionState);

                runspacePool.SetMaxRunspaces(1);
                runspacePool.ThreadOptions = PSThreadOptions.ReuseThread;

                // Open the runspace pool here so we can deterministically handle the PSModulePath change issue
                using (PSModulePathPreserver.Take())
                {
                    runspacePool.Open();
                }

                return runspacePool;
            }
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

        /// <summary>
        /// Struct to manage a call that may change the PSModulePath, so that it can be safely reset afterward.
        /// </summary>
        /// <remarks>
        /// If the user manages to set the module path at the same time, using this struct may override that.
        /// But this happening is less likely than the current issue where the PSModulePath is always reset.
        /// </remarks>
        private struct PSModulePathPreserver : IDisposable
        {
            private static object s_psModulePathMutationLock = new object();

            public static PSModulePathPreserver Take()
            {
                Monitor.Enter(s_psModulePathMutationLock);
                return new PSModulePathPreserver(Environment.GetEnvironmentVariable("PSModulePath"));
            }

            private readonly string _psModulePath;

            private PSModulePathPreserver(string psModulePath)
            {
                _psModulePath = psModulePath;
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable("PSModulePath", _psModulePath);
                Monitor.Exit(s_psModulePathMutationLock);
            }
        }
    }
}
