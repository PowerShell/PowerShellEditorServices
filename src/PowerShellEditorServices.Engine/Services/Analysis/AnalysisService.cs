﻿//
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

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a high-level service for performing semantic analysis
    /// of PowerShell scripts.
    /// </summary>
    public class AnalysisService : IDisposable
    {
        #region Static fields

        /// <summary>
        /// Defines the list of Script Analyzer rules to include by default if
        /// no settings file is specified.
        /// </summary>
        private static readonly string[] s_includedRules = {
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
        /// An empty diagnostic result to return when a script fails analysis.
        /// </summary>
        private static readonly PSObject[] s_emptyDiagnosticResult = new PSObject[0];

        private static readonly string[] s_emptyGetRuleResult = new string[0];

        /// <summary>
        /// The indentation to add when the logger lists errors.
        /// </summary>
        private static readonly string s_indentJoin = Environment.NewLine + "    ";

        #endregion // Static fields

        #region Private Fields

        /// <summary>
        /// Maximum number of runspaces we allow to be in use for script analysis.
        /// </summary>
        private const int NumRunspaces = 1;

        /// <summary>
        /// Name of the PSScriptAnalyzer module, to be used for PowerShell module interactions.
        /// </summary>
        private const string PSSA_MODULE_NAME = "PSScriptAnalyzer";

        /// <summary>
        /// Provides logging.
        /// </summary>
        private ILogger _logger;

        /// <summary>
        /// Runspace pool to generate runspaces for script analysis and handle
        /// ansynchronous analysis requests.
        /// </summary>
        private RunspacePool _analysisRunspacePool;

        /// <summary>
        /// Info object describing the PSScriptAnalyzer module that has been loaded in
        /// to provide analysis services.
        /// </summary>
        private PSModuleInfo _pssaModuleInfo;

        #endregion // Private Fields

        #region Properties

        /// <summary>
        /// Set of PSScriptAnalyzer rules used for analysis.
        /// </summary>
        public string[] ActiveRules { get; set; }

        /// <summary>
        /// Gets or sets the path to a settings file (.psd1)
        /// containing PSScriptAnalyzer settings.
        /// </summary>
        public string SettingsPath { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Construct a new AnalysisService object.
        /// </summary>
        /// <param name="analysisRunspacePool">
        /// The runspace pool with PSScriptAnalyzer module loaded that will handle
        /// analysis tasks.
        /// </param>
        /// <param name="pssaSettingsPath">
        /// The path to the PSScriptAnalyzer settings file to handle analysis settings.
        /// </param>
        /// <param name="activeRules">An array of rules to be used for analysis.</param>
        /// <param name="logger">Maintains logs for the analysis service.</param>
        /// <param name="pssaModuleInfo">
        /// Optional module info of the loaded PSScriptAnalyzer module. If not provided,
        /// the analysis service will populate it, but it can be given here to save time.
        /// </param>
        private AnalysisService(
            RunspacePool analysisRunspacePool,
            string pssaSettingsPath,
            IEnumerable<string> activeRules,
            ILogger logger,
            PSModuleInfo pssaModuleInfo = null)
        {
            _analysisRunspacePool = analysisRunspacePool;
            SettingsPath = pssaSettingsPath;
            ActiveRules = activeRules.ToArray();
            _logger = logger;
            _pssaModuleInfo = pssaModuleInfo;
        }

        #endregion // constructors

        #region Public Methods

        /// <summary>
        /// Factory method for producing AnalysisService instances. Handles loading of the PSScriptAnalyzer module
        /// and runspace pool instantiation before creating the service instance.
        /// </summary>
        /// <param name="settingsPath">Path to the PSSA settings file to be used for this service instance.</param>
        /// <param name="logger">EditorServices logger for logging information.</param>
        /// <returns>
        /// A new analysis service instance with a freshly imported PSScriptAnalyzer module and runspace pool.
        /// Returns null if problems occur. This method should never throw.
        /// </returns>
        public static AnalysisService Create(string settingsPath, ILogger logger)
        {
            try
            {
                RunspacePool analysisRunspacePool;
                PSModuleInfo pssaModuleInfo;
                try
                {
                    // Try and load a PSScriptAnalyzer module with the required version
                    // by looking on the script path. Deep down, this internally runs Get-Module -ListAvailable,
                    // so we'll use this to check whether such a module exists
                    analysisRunspacePool = CreatePssaRunspacePool(out pssaModuleInfo);

                }
                catch (Exception e)
                {
                    throw new AnalysisServiceLoadException("PSScriptAnalyzer runspace pool could not be created", e);
                }

                if (analysisRunspacePool == null)
                {
                    throw new AnalysisServiceLoadException("PSScriptAnalyzer runspace pool failed to be created");
                }

                // Having more than one runspace doesn't block code formatting if one
                // runspace is occupied for diagnostics
                analysisRunspacePool.SetMaxRunspaces(NumRunspaces);
                analysisRunspacePool.ThreadOptions = PSThreadOptions.ReuseThread;
                analysisRunspacePool.Open();

                var analysisService = new AnalysisService(
                    analysisRunspacePool,
                    settingsPath,
                    s_includedRules,
                    logger,
                    pssaModuleInfo);

                // Log what features are available in PSSA here
                analysisService.LogAvailablePssaFeatures();

                return analysisService;
            }
            catch (AnalysisServiceLoadException e)
            {
                logger.LogWarning("PSScriptAnalyzer cannot be imported, AnalysisService will be disabled", e);
                return null;
            }
            catch (Exception e)
            {
                logger.LogWarning("AnalysisService could not be started due to an unexpected exception", e);
                return null;
            }
        }

        /// <summary>
        /// Get PSScriptAnalyzer settings hashtable for PSProvideCommentHelp rule.
        /// </summary>
        /// <param name="enable">Enable the rule.</param>
        /// <param name="exportedOnly">Analyze only exported functions/cmdlets.</param>
        /// <param name="blockComment">Use block comment or line comment.</param>
        /// <param name="vscodeSnippetCorrection">Return a vscode snipped correction should be returned.</param>
        /// <param name="placement">Place comment help at the given location relative to the function definition.</param>
        /// <returns>A PSScriptAnalyzer settings hashtable.</returns>
        public static Hashtable GetCommentHelpRuleSettings(
            bool enable,
            bool exportedOnly,
            bool blockComment,
            bool vscodeSnippetCorrection,
            string placement)
        {
            var settings = new Dictionary<string, Hashtable>();
            var ruleSettings = new Hashtable();
            ruleSettings.Add("Enable", enable);
            ruleSettings.Add("ExportedOnly", exportedOnly);
            ruleSettings.Add("BlockComment", blockComment);
            ruleSettings.Add("VSCodeSnippetCorrection", vscodeSnippetCorrection);
            ruleSettings.Add("Placement", placement);
            settings.Add("PSProvideCommentHelp", ruleSettings);
            return GetPSSASettingsHashtable(settings);
        }

        /// <summary>
        /// Construct a PSScriptAnalyzer settings hashtable
        /// </summary>
        /// <param name="ruleSettingsMap">A settings hashtable</param>
        /// <returns></returns>
        public static Hashtable GetPSSASettingsHashtable(IDictionary<string, Hashtable> ruleSettingsMap)
        {
            var hashtable = new Hashtable();
            var ruleSettingsHashtable = new Hashtable();

            hashtable["IncludeRules"] = ruleSettingsMap.Keys.ToArray<object>();
            hashtable["Rules"] = ruleSettingsHashtable;

            foreach (var kvp in ruleSettingsMap)
            {
                ruleSettingsHashtable.Add(kvp.Key, kvp.Value);
            }

            return hashtable;
        }

        /// <summary>
        /// Perform semantic analysis on the given ScriptFile and returns
        /// an array of ScriptFileMarkers.
        /// </summary>
        /// <param name="file">The ScriptFile which will be analyzed for semantic markers.</param>
        /// <returns>An array of ScriptFileMarkers containing semantic analysis results.</returns>
        public async Task<List<ScriptFileMarker>> GetSemanticMarkersAsync(ScriptFile file)
        {
            return await GetSemanticMarkersAsync<string>(file, ActiveRules, SettingsPath);
        }

        /// <summary>
        /// Perform semantic analysis on the given ScriptFile with the given settings.
        /// </summary>
        /// <param name="file">The ScriptFile to be analyzed.</param>
        /// <param name="settings">ScriptAnalyzer settings</param>
        /// <returns></returns>
        public async Task<List<ScriptFileMarker>> GetSemanticMarkersAsync(ScriptFile file, Hashtable settings)
        {
            return await GetSemanticMarkersAsync<Hashtable>(file, null, settings);
        }

        /// <summary>
        /// Perform semantic analysis on the given script with the given settings.
        /// </summary>
        /// <param name="scriptContent">The script content to be analyzed.</param>
        /// <param name="settings">ScriptAnalyzer settings</param>
        /// <returns></returns>
        public async Task<List<ScriptFileMarker>> GetSemanticMarkersAsync(
           string scriptContent,
           Hashtable settings)
        {
            return await GetSemanticMarkersAsync<Hashtable>(scriptContent, null, settings);
        }

        /// <summary>
        /// Returns a list of builtin-in PSScriptAnalyzer rules
        /// </summary>
        public IEnumerable<string> GetPSScriptAnalyzerRules()
        {
            PowerShellResult getRuleResult = InvokePowerShell("Get-ScriptAnalyzerRule");
            if (getRuleResult == null)
            {
                _logger.LogWarning("Get-ScriptAnalyzerRule returned null result");
                return s_emptyGetRuleResult;
            }

            var ruleNames = new List<string>();
            foreach (var rule in getRuleResult.Output)
            {
                ruleNames.Add((string)rule.Members["RuleName"].Value);
            }

            return ruleNames;
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
            Hashtable settings,
            int[] rangeList)
        {
            // We cannot use Range type therefore this workaround of using -1 default value.
            // Invoke-Formatter throws a ParameterBinderValidationException if the ScriptDefinition is an empty string.
            if (string.IsNullOrEmpty(scriptDefinition))
            {
                return null;
            }

            var argsDict = new Dictionary<string, object> {
                    {"ScriptDefinition", scriptDefinition},
                    {"Settings", settings}
            };
            if (rangeList != null)
            {
                argsDict.Add("Range", rangeList);
            }

            PowerShellResult result = await InvokePowerShellAsync("Invoke-Formatter", argsDict);

            if (result == null)
            {
                _logger.LogError("Formatter returned null result");
                return null;
            }

            if (result.HasErrors)
            {
                var errorBuilder = new StringBuilder().Append(s_indentJoin);
                foreach (ErrorRecord err in result.Errors)
                {
                    errorBuilder.Append(err).Append(s_indentJoin);
                }
                _logger.LogWarning($"Errors found while formatting file: {errorBuilder}");
                return null;
            }

            foreach (PSObject resultObj in result.Output)
            {
                string formatResult = resultObj?.BaseObject as string;
                if (formatResult != null)
                {
                    return formatResult;
                }
            }

            return null;
        }

        #endregion // public methods

        #region Private Methods

        private async Task<List<ScriptFileMarker>> GetSemanticMarkersAsync<TSettings>(
            ScriptFile file,
            string[] rules,
            TSettings settings) where TSettings : class
        {
            if (file.IsAnalysisEnabled)
            {
                return await GetSemanticMarkersAsync<TSettings>(
                    file.Contents,
                    rules,
                    settings);
            }
            else
            {
                // Return an empty marker list
                return new List<ScriptFileMarker>();
            }
        }

        private async Task<List<ScriptFileMarker>> GetSemanticMarkersAsync<TSettings>(
            string scriptContent,
            string[] rules,
            TSettings settings) where TSettings : class
        {
            if ((typeof(TSettings) == typeof(string) || typeof(TSettings) == typeof(Hashtable))
                && (rules != null || settings != null))
            {
                var scriptFileMarkers = await GetDiagnosticRecordsAsync(scriptContent, rules, settings);
                return scriptFileMarkers.Select(ScriptFileMarker.FromDiagnosticRecord).ToList();
            }
            else
            {
                // Return an empty marker list
                return new List<ScriptFileMarker>();
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

            // If we already know the module that was imported, save some work
            if (_pssaModuleInfo == null)
            {
                PowerShellResult getModuleResult = InvokePowerShell(
                    "Get-Module",
                    new Dictionary<string, object>{ {"Name", PSSA_MODULE_NAME} });

                if (getModuleResult == null)
                {
                    throw new AnalysisServiceLoadException("Get-Module call to find PSScriptAnalyzer module failed");
                }

                _pssaModuleInfo = getModuleResult.Output
                    .Select(m => m.BaseObject)
                    .OfType<PSModuleInfo>()
                    .FirstOrDefault();
            }

            if (_pssaModuleInfo == null)
            {
                throw new AnalysisServiceLoadException("Unable to find loaded PSScriptAnalyzer module for logging");
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

        private async Task<PSObject[]> GetDiagnosticRecordsAsync<TSettings>(
             string scriptContent,
             string[] rules,
             TSettings settings) where TSettings : class
        {
            var diagnosticRecords = s_emptyDiagnosticResult;

            // When a new, empty file is created there are by definition no issues.
            // Furthermore, if you call Invoke-ScriptAnalyzer with an empty ScriptDefinition
            // it will generate a ParameterBindingValidationException.
            if (string.IsNullOrEmpty(scriptContent))
            {
                return diagnosticRecords;
            }

            if (typeof(TSettings) == typeof(string) || typeof(TSettings) == typeof(Hashtable))
            {
                //Use a settings file if one is provided, otherwise use the default rule list.
                string settingParameter;
                object settingArgument;
                if (settings != null)
                {
                    settingParameter = "Settings";
                    settingArgument = settings;
                }
                else
                {
                    settingParameter = "IncludeRule";
                    settingArgument = rules;
                }

                PowerShellResult result = await InvokePowerShellAsync(
                    "Invoke-ScriptAnalyzer",
                    new Dictionary<string, object>
                    {
                        { "ScriptDefinition", scriptContent },
                        { settingParameter, settingArgument },
                        // We ignore ParseErrors from PSSA because we already send them when we parse the file.
                        { "Severity", new [] { ScriptFileMarkerLevel.Error, ScriptFileMarkerLevel.Information, ScriptFileMarkerLevel.Warning }}
                    });

                diagnosticRecords = result?.Output;
            }

            _logger.LogDebug(String.Format("Found {0} violations", diagnosticRecords.Count()));

            return diagnosticRecords;
        }

        private PowerShellResult InvokePowerShell(string command, IDictionary<string, object> paramArgMap = null)
        {
            using (var powerShell = System.Management.Automation.PowerShell.Create())
            {
                powerShell.RunspacePool = _analysisRunspacePool;
                powerShell.AddCommand(command);
                if (paramArgMap != null)
                {
                    foreach (KeyValuePair<string, object> kvp in paramArgMap)
                    {
                        powerShell.AddParameter(kvp.Key, kvp.Value);
                    }
                }

                PowerShellResult result = null;
                try
                {
                    PSObject[] output = powerShell.Invoke().ToArray();
                    ErrorRecord[] errors = powerShell.Streams.Error.ToArray();
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

        private async Task<PowerShellResult> InvokePowerShellAsync(string command, IDictionary<string, object> paramArgMap = null)
        {
            var task = Task.Run(() =>
            {
                return InvokePowerShell(command, paramArgMap);
            });

            return await task;
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
                    // Get the latest version of PSScriptAnalyzer we can find
                    pssaModuleInfo = ps.Invoke()?
                        .Select(psObj => psObj.BaseObject)
                        .OfType<PSModuleInfo>()
                        .OrderByDescending(moduleInfo => moduleInfo.Version)
                        .FirstOrDefault();
                }
                catch (Exception e)
                {
                    throw new AnalysisServiceLoadException("Unable to find PSScriptAnalyzer module on the module path", e);
                }

                if (pssaModuleInfo == null)
                {
                    throw new AnalysisServiceLoadException("Unable to find PSScriptAnalyzer module on the module path");
                }

                // Create a base session state with PSScriptAnalyzer loaded
                InitialSessionState sessionState;
                if (Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1") {
                    sessionState = InitialSessionState.CreateDefault();
                } else {
                    sessionState = InitialSessionState.CreateDefault2();
                }
                sessionState.ImportPSModule(new [] { pssaModuleInfo.ModuleBase });

                // RunspacePool takes care of queuing commands for us so we do not
                // need to worry about executing concurrent commands
                return RunspaceFactory.CreateRunspacePool(sessionState);
            }
        }

        #endregion //private methods

        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Dispose of this object.
        /// </summary>
        /// <param name="disposing">True if the method is called by the Dispose method, false if called by the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _analysisRunspacePool.Dispose();
                    _analysisRunspacePool = null;
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Clean up all internal resources and dispose of the analysis service.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion

        /// <summary>
        /// Wraps the result of an execution of PowerShell to send back through
        /// asynchronous calls.
        /// </summary>
        private class PowerShellResult
        {
            public PowerShellResult(
                PSObject[] output,
                ErrorRecord[] errors,
                bool hasErrors)
            {
                Output = output;
                Errors = errors;
                HasErrors = hasErrors;
            }

            public PSObject[] Output { get; }

            public ErrorRecord[] Errors { get; }

            public bool HasErrors { get; }
        }
    }

    /// <summary>
    /// Class to catch known failure modes for starting the AnalysisService.
    /// </summary>
    public class AnalysisServiceLoadException : Exception
    {
        /// <summary>
        /// Instantiate an AnalysisService error based on a simple message.
        /// </summary>
        /// <param name="message">The message to display to the user detailing the error.</param>
        public AnalysisServiceLoadException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate an AnalysisService error based on another error that occurred internally.
        /// </summary>
        /// <param name="message">The message to display to the user detailing the error.</param>
        /// <param name="innerException">The inner exception that occurred to trigger this error.</param>
        public AnalysisServiceLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
