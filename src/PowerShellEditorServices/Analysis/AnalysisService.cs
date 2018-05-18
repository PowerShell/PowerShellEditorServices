﻿//
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

        private IPsesLogger _logger;
        private RunspacePool _analysisRunspacePool;

        private bool _hasScriptAnalyzerModule;

        private string[] activeRules;
        private string settingsPath;

        /// <summary>
        /// Defines the list of Script Analyzer rules to include by default if
        /// no settings file is specified.
        /// </summary>
        private static readonly string[] IncludedRules = new string[]
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
        /// Set of PSScriptAnalyzer rules used for analysis
        /// </summary>
        public string[] ActiveRules
        {
            get
            {
                return activeRules;
            }

            set
            {
                activeRules = value;
            }
        }

        /// <summary>
        /// Gets or sets the path to a settings file (.psd1)
        /// containing PSScriptAnalyzer settings.
        /// </summary>
        public string SettingsPath
        {
            get
            {
                return settingsPath;
            }
            set
            {
                settingsPath = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the AnalysisService class.
        /// </summary>
        /// <param name="settingsPath">Path to a PSScriptAnalyzer settings file.</param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public AnalysisService(string settingsPath, IPsesLogger logger)
        {
            this._logger = logger;

            try
            {
                this.SettingsPath = settingsPath;

                if (!(_hasScriptAnalyzerModule = VerifyPSScriptAnalyzerAvailable()))
                {
                    throw new Exception("PSScriptAnalyzer module not available");
                }

                // Create a base session state with PSScriptAnalyzer loaded
                InitialSessionState sessionState = InitialSessionState.CreateDefault2();
                sessionState.ImportPSModule(new [] { PSSA_MODULE_NAME });

                // runspacepool takes care of queuing commands for us so we do not
                // need to worry about executing concurrent commands
                this._analysisRunspacePool = RunspaceFactory.CreateRunspacePool(sessionState);

                // having more than one runspace doesn't block code formatting if one
                // runspace is occupied for diagnostics
                this._analysisRunspacePool.SetMaxRunspaces(NumRunspaces);
                this._analysisRunspacePool.ThreadOptions = PSThreadOptions.ReuseThread;
                this._analysisRunspacePool.Open();

                ActiveRules = IncludedRules.ToArray();
                EnumeratePSScriptAnalyzerCmdlets();
                EnumeratePSScriptAnalyzerRules();
            }
            catch (Exception e)
            {
                var sb = new StringBuilder();
                sb.AppendLine("PSScriptAnalyzer cannot be imported, AnalysisService will be disabled.");
                sb.AppendLine(e.Message);
                this._logger.Write(LogLevel.Warning, sb.ToString());
            }
        }

        #endregion // constructors

        #region Public Methods

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
            return await GetSemanticMarkersAsync<string>(file, activeRules, settingsPath);
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
            if (_hasScriptAnalyzerModule)
            {
                var ruleObjects = InvokePowerShell("Get-ScriptAnalyzerRule", new Dictionary<string, object>());
                foreach (var rule in ruleObjects)
                {
                    ruleNames.Add((string)rule.Members["RuleName"].Value);
                }
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
            // we cannot use Range type therefore this workaround of using -1 default value
            if (!_hasScriptAnalyzerModule)
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

            var result = await InvokePowerShellAsync("Invoke-Formatter", argsDict);
            return result?.Select(r => r?.ImmediateBaseObject as string).FirstOrDefault();
        }

        /// <summary>
        /// Disposes the runspace being used by the analysis service.
        /// </summary>
        public void Dispose()
        {
            if (this._analysisRunspacePool != null)
            {
                this._analysisRunspacePool.Close();
                this._analysisRunspacePool.Dispose();
                this._analysisRunspacePool = null;
            }
        }

        #endregion // public methods

        #region Private Methods

        private async Task<ScriptFileMarker[]> GetSemanticMarkersAsync<TSettings>(
            ScriptFile file,
            string[] rules,
            TSettings settings) where TSettings : class
        {
            if (_hasScriptAnalyzerModule
                && file.IsAnalysisEnabled)
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

        private void EnumeratePSScriptAnalyzerCmdlets()
        {
            if (_hasScriptAnalyzerModule)
            {
                var sb = new StringBuilder();
                var commands = InvokePowerShell(
                "Get-Command",
                new Dictionary<string, object>
                {
                    {"Module", "PSScriptAnalyzer"}
                });

                var commandNames = commands?
                    .Select(c => c.ImmediateBaseObject as CmdletInfo)
                    .Where(c => c != null)
                    .Select(c => c.Name) ?? Enumerable.Empty<string>();

                sb.AppendLine("The following cmdlets are available in the imported PSScriptAnalyzer module:");
                sb.AppendLine(String.Join(Environment.NewLine, commandNames.Select(s => "    " + s)));
                this._logger.Write(LogLevel.Verbose, sb.ToString());
            }
        }

        private void EnumeratePSScriptAnalyzerRules()
        {
            if (_hasScriptAnalyzerModule)
            {
                var rules = GetPSScriptAnalyzerRules();
                var sb = new StringBuilder();
                sb.AppendLine("Available PSScriptAnalyzer Rules:");
                foreach (var rule in rules)
                {
                    sb.AppendLine(rule);
                }

                this._logger.Write(LogLevel.Verbose, sb.ToString());
            }
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

            if (_hasScriptAnalyzerModule
                && (typeof(TSettings) == typeof(string)
                    || typeof(TSettings) == typeof(Hashtable)))
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

            this._logger.Write(
                LogLevel.Verbose,
                String.Format("Found {0} violations", diagnosticRecords.Count()));

            return diagnosticRecords;
        }


        private PSObject[] InvokePowerShell(string command, IDictionary<string, object> paramArgMap)
        {
            using (var powerShell = System.Management.Automation.PowerShell.Create())
            {
                powerShell.RunspacePool = this._analysisRunspacePool;
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
                    this._logger.Write(LogLevel.Error, ex.Message);
                }
                catch (CmdletInvocationException ex)
                {
                    // We do not want to crash EditorServices for exceptions caused by cmdlet invocation.
                    // Two main reasons that cause the exception are:
                    // * PSCmdlet.WriteOutput being called from another thread than Begin/Process
                    // * CompositionContainer.ComposeParts complaining that "...Only one batch can be composed at a time"
                    this._logger.Write(LogLevel.Error, ex.Message);
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

        private bool VerifyPSScriptAnalyzerAvailable()
        {
            using (var ps = System.Management.Automation.PowerShell.Create())
            {
                ps.AddCommand("Get-Module")
                    .AddParameter("ListAvailable")
                    .AddParameter("Name", PSSA_MODULE_NAME);

                try
                {
                    return ps.Invoke()?.Any() ?? false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        #endregion //private methods
    }
}
