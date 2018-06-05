//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Console;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.IO;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a high-level service for performing semantic analysis
    /// of PowerShell scripts.
    /// </summary>
    public class AnalysisService : IDisposable
    {
        #region Private Fields

        private const int NumRunspaces = 1;

        private const string PSSA_MODULE_NAME = "PSScriptAnalyzer";

        private static readonly Version s_pssaMinimumVersion = new Version(1, 16);

        private ILogger _logger;
        private RunspacePool _analysisRunspacePool;

        /// <summary>
        /// Defines the list of Script Analyzer rules to include by default if
        /// no settings file is specified.
        /// </summary>
        private static readonly string[] s_includedRules = new string[]
        {
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
            "PSUseDeclaredVarsMoreThanAssignments"
        };

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

        private AnalysisService(RunspacePool analysisRunspacePool, string pssaSettingsPath, IEnumerable<string> activeRules, ILogger logger)
        {
            _analysisRunspacePool = analysisRunspacePool;
            SettingsPath = pssaSettingsPath;
            ActiveRules = activeRules.ToArray();
            _logger = logger;
        }

        #endregion // constructors

        #region Public Methods

        /// <summary>
        /// Factory method for producing AnalysisService instances. Handles loading of the PSScriptAnalyzer module
        /// and runspace pool instantiation before creating the service instance.
        /// </summary>
        /// <param name="settingsPath">Path to the PSSA settings file to be used for this service instance.</param>
        /// <param name="logger">EditorServices logger for logging information.</param>
        /// <returns>A new analysis service instance with a freshly imported PSScriptAnalyzer module and runspace pool.</returns>
        public static AnalysisService Create(string settingsPath, ILogger logger)
        {
            try
            {
                RunspacePool analysisRunspacePool;
                try
                {
                    // Try and load a PSScriptAnalyzer module with the required version
                    // by looking on the script path. Deep down, this internally runs Get-Module -ListAvailable,
                    // so we'll use this to check whether such a module exists
                    analysisRunspacePool = CreatePssaRunspacePool();

                }
                catch (Exception e)
                {
                    throw new Exception("PSScriptAnalyzer module of the required minimum version not available", e);
                }

                if (analysisRunspacePool == null)
                {
                    throw new Exception("PSScriptAnalyzer module of the required minimum version not available");
                }

                // Having more than one runspace doesn't block code formatting if one
                // runspace is occupied for diagnostics
                analysisRunspacePool.SetMaxRunspaces(NumRunspaces);
                analysisRunspacePool.ThreadOptions = PSThreadOptions.ReuseThread;
                analysisRunspacePool.Open();

                var analysisService = new AnalysisService(analysisRunspacePool, settingsPath, s_includedRules, logger);

                // Log what features are available in PSSA here
                analysisService.LogAvailablePssaFeatures();

                return analysisService;
            }
            catch (Exception e)
            {
                logger.WriteException("PSScriptAnalyzer cannot be imported, AnalysisService will be disabled.", e);
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
        public async Task<ScriptFileMarker[]> GetSemanticMarkersAsync(ScriptFile file)
        {
            return await GetSemanticMarkersAsync<string>(file, ActiveRules, SettingsPath);
        }

        /// <summary>
        /// Perform semantic analysis on the given ScriptFile with the given settings.
        /// </summary>
        /// <param name="file">The ScriptFile to be analyzed.</param>
        /// <param name="settings">ScriptAnalyzer settings</param>
        /// <returns></returns>
        public async Task<ScriptFileMarker[]> GetSemanticMarkersAsync(ScriptFile file, Hashtable settings)
        {
            return await GetSemanticMarkersAsync<Hashtable>(file, null, settings);
        }

        /// <summary>
        /// Perform semantic analysis on the given script with the given settings.
        /// </summary>
        /// <param name="scriptContent">The script content to be analyzed.</param>
        /// <param name="settings">ScriptAnalyzer settings</param>
        /// <returns></returns>
        public async Task<ScriptFileMarker[]> GetSemanticMarkersAsync(
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
            List<string> ruleNames = new List<string>();
            var ruleObjects = InvokePowerShell("Get-ScriptAnalyzerRule", new Dictionary<string, object>());
            foreach (var rule in ruleObjects)
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
        public async Task<string> Format(
            string scriptDefinition,
            Hashtable settings,
            int[] rangeList)
        {
            var argsDict = new Dictionary<string, object> {
                    {"ScriptDefinition", scriptDefinition},
                    {"Settings", settings}
            };
            if (rangeList != null)
            {
                argsDict.Add("Range", rangeList);
            }

            var result = await InvokePowerShellAsync("Invoke-Formatter", argsDict);
            return result?.Select(r => r?.ImmediateBaseObject as string).FirstOrDefault();
        }

        /// <summary>
        /// Disposes the runspace being used by the analysis service.
        /// </summary>
        public void Dispose()
        {
            if (_analysisRunspacePool != null)
            {
                _analysisRunspacePool.Close();
                _analysisRunspacePool.Dispose();
                _analysisRunspacePool = null;
            }
        }

        #endregion // public methods

        #region Private Methods

        private async Task<ScriptFileMarker[]> GetSemanticMarkersAsync<TSettings>(
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
                return new ScriptFileMarker[0];
            }
        }

        private async Task<ScriptFileMarker[]> GetSemanticMarkersAsync<TSettings>(
            string scriptContent,
            string[] rules,
            TSettings settings) where TSettings : class
        {
            if ((typeof(TSettings) == typeof(string) || typeof(TSettings) == typeof(Hashtable))
                && (rules != null || settings != null))
            {
                var scriptFileMarkers = await GetDiagnosticRecordsAsync(scriptContent, rules, settings);
                return scriptFileMarkers.Select(ScriptFileMarker.FromDiagnosticRecord).ToArray();
            }
            else
            {
                // Return an empty marker list
                return new ScriptFileMarker[0];
            }
        }

        /// <summary>
        /// Log the features available from the PSScriptAnalyzer module that has been imported
        /// for use with the AnalysisService.
        /// </summary>
        private void LogAvailablePssaFeatures()
        {
            // Save ourselves some work here
            var featureLogLevel = LogLevel.Verbose;
            if (_logger.MinimumConfiguredLogLevel > featureLogLevel)
            {
                return;
            }

            PSObject[] modules = InvokePowerShell(
                "Get-Module",
                new Dictionary<string, object>{ {"Name", PSSA_MODULE_NAME} });

            PSModuleInfo pssaModuleInfo = modules
                .Select(m => m.BaseObject)
                .OfType<PSModuleInfo>()
                .FirstOrDefault();

            if (pssaModuleInfo == null)
            {
                throw new Exception("Unable to find loaded PSScriptAnalyzer module for logging");
            }

            var sb = new StringBuilder();
            sb.AppendLine("PSScriptAnalyzer successfully imported:");

            // Log version
            sb.AppendLine($"    Version: {pssaModuleInfo.Version}");

            // Log exported cmdlets
            sb.AppendLine("    Exported Cmdlets:");
            foreach (string cmdletName in pssaModuleInfo.ExportedCmdlets.Keys.OrderBy(name => name))
            {
                sb.AppendLine("        " + cmdletName);
            }

            // Log available rules
            sb.AppendLine("    Available Rules:");
            foreach (string ruleName in GetPSScriptAnalyzerRules())
            {
                sb.AppendLine("        " + ruleName);
            }

            _logger.Write(featureLogLevel, sb.ToString());
        }

        private async Task<PSObject[]> GetDiagnosticRecordsAsync<TSettings>(
             string scriptContent,
             string[] rules,
             TSettings settings) where TSettings : class
        {
            var diagnosticRecords = new PSObject[0];

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

                diagnosticRecords = await InvokePowerShellAsync(
                    "Invoke-ScriptAnalyzer",
                    new Dictionary<string, object>
                    {
                        { "ScriptDefinition", scriptContent },
                        { settingParameter, settingArgument }
                    });
            }

            _logger.Write(
                LogLevel.Verbose,
                String.Format("Found {0} violations", diagnosticRecords.Count()));

            return diagnosticRecords;
        }

        private PSObject[] InvokePowerShell(string command, IDictionary<string, object> paramArgMap)
        {
            using (var powerShell = System.Management.Automation.PowerShell.Create())
            {
                powerShell.RunspacePool = _analysisRunspacePool;
                powerShell.AddCommand(command);
                foreach (var kvp in paramArgMap)
                {
                    powerShell.AddParameter(kvp.Key, kvp.Value);
                }

                var result = new PSObject[0];
                try
                {
                    result = powerShell.Invoke()?.ToArray();
                }
                catch (CommandNotFoundException ex)
                {
                    // This exception is possible if the module path loaded
                    // is wrong even though PSScriptAnalyzer is available as a module
                    _logger.Write(LogLevel.Error, ex.Message);
                }
                catch (CmdletInvocationException ex)
                {
                    // We do not want to crash EditorServices for exceptions caused by cmdlet invocation.
                    // Two main reasons that cause the exception are:
                    // * PSCmdlet.WriteOutput being called from another thread than Begin/Process
                    // * CompositionContainer.ComposeParts complaining that "...Only one batch can be composed at a time"
                    _logger.Write(LogLevel.Error, ex.Message);
                }

                return result;
            }
        }

        private async Task<PSObject[]> InvokePowerShellAsync(string command, IDictionary<string, object> paramArgMap)
        {
            var task = Task.Run(() =>
            {
                return InvokePowerShell(command, paramArgMap);
            });

            return await task;
        }

        /// <summary>
        /// Create a new runspace pool around a PSScriptAnalyzer module for asynchronous script analysis tasks.
        /// This uses the PowerShell module API to load the first PSScriptAnalyzer module on the module path with
        /// with the required minimum version and then creates a runspace pool with that module loaded in the initial
        /// session state.
        /// </summary>
        /// <returns>A runspace pool with PSScriptAnalyzer loaded for running script analysis tasks.</returns>
#if PowerShellv5
        private static RunspacePool CreatePssaRunspacePool()
        {
            // Use public but unfriendly APIs to import a PSScriptAnalyzer module with the needed minimum version
            var pssaModuleSpec = new ModuleSpecification(new Hashtable()
            {
                { "ModuleName", PSSA_MODULE_NAME },
                { "ModuleVersion", s_pssaMinimumVersion }
            });

            // Create a base session state with PSScriptAnalyzer loaded
            InitialSessionState sessionState = InitialSessionState.CreateDefault2();
            sessionState.ImportPSModule(new [] { pssaModuleSpec });

            // RunspacePool takes care of queuing commands for us so we do not
            // need to worry about executing concurrent commands
            return RunspaceFactory.CreateRunspacePool(sessionState);
        }
#else
        private static RunspacePool CreatePssaRunspacePool()
        {
            using (var ps = System.Management.Automation.PowerShell.Create())
            {
                ps.AddCommand("Get-Module")
                    .AddParameter("ListAvailable")
                    .AddParameter("Name", PSSA_MODULE_NAME);

                PSModuleInfo pssaModuleInfo;
                try
                {
                    pssaModuleInfo = ps.Invoke()?
                        .Select(psObj => psObj.BaseObject)
                        .OfType<PSModuleInfo>()
                        .FirstOrDefault();
                }
                catch (Exception e)
                {
                    throw new Exception("Unable to find PSScriptAnalyzer module", e);
                }

                if (pssaModuleInfo == null)
                {
                    throw new Exception("Unable to find PSScriptAnalyzer module");
                }

                // Create a base session state with PSScriptAnalyzer loaded
                InitialSessionState sessionState = InitialSessionState.CreateDefault2();
                sessionState.ImportPSModule(new [] { pssaModuleInfo.ModuleBase });

                // RunspacePool takes care of queuing commands for us so we do not
                // need to worry about executing concurrent commands
                return RunspaceFactory.CreateRunspacePool(sessionState);
            }
        }
#endif

        #endregion //private methods
    }
}
