//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Console;
using System.Management.Automation;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a high-level service for performing semantic analysis
    /// of PowerShell scripts.
    /// </summary>
    public class AnalysisService : IDisposable
    {
        #region Private Fields

        private Runspace analysisRunspace;
        private PSModuleInfo scriptAnalyzerModuleInfo;
        private Object runspaceLock;
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
            "PSUseDeclaredVarsMoreThanAssigments"
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
                lock (runspaceLock)
                {
                    activeRules = value;
                }
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
                lock (runspaceLock)
                {
                    settingsPath = value;
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the AnalysisService class.
        /// </summary>
        /// <param name="consoleHost">An object that implements IConsoleHost in which to write errors/warnings
        /// from analyzer.</param>
        /// <param name="settingsPath">Path to a PSScriptAnalyzer settings file.</param>
        public AnalysisService(IConsoleHost consoleHost, string settingsPath = null)
        {
            try
            {
                this.runspaceLock = new Object();
                this.SettingsPath = settingsPath;
                this.analysisRunspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
                this.analysisRunspace.ThreadOptions = PSThreadOptions.ReuseThread;
                this.analysisRunspace.Open();
                ActiveRules = IncludedRules.ToArray();
                InitializePSScriptAnalyzer();
            }
            catch (Exception e)
            {
                var sb = new StringBuilder();
                sb.AppendLine("PSScriptAnalyzer cannot be imported, AnalysisService will be disabled.");
                sb.AppendLine(e.Message);
                Logger.Write(LogLevel.Warning, sb.ToString());
            }
        }

        #endregion // constructors

        #region Public Methods

        /// <summary>
        /// Perform semantic analysis on the given ScriptFile and returns
        /// an array of ScriptFileMarkers.
        /// </summary>
        /// <param name="file">The ScriptFile which will be analyzed for semantic markers.</param>
        /// <returns>An array of ScriptFileMarkers containing semantic analysis results.</returns>
        public ScriptFileMarker[] GetSemanticMarkers(ScriptFile file)
        {
            return GetSemanticMarkers(file, activeRules, settingsPath);
        }

        /// <summary>
        /// Perform semantic analysis on the given ScriptFile with the given settings.
        /// </summary>
        /// <param name="file">The ScriptFile to be analyzed.</param>
        /// <param name="settings">ScriptAnalyzer settings</param>
        /// <returns></returns>
        public ScriptFileMarker[] GetSemanticMarkers(ScriptFile file, Hashtable settings)
        {
            return GetSemanticMarkers<Hashtable>(file, null, settings);
        }

        /// <summary>
        /// Returns a list of builtin-in PSScriptAnalyzer rules
        /// </summary>
        public IEnumerable<string> GetPSScriptAnalyzerRules()
        {
            List<string> ruleNames = new List<string>();
            if (scriptAnalyzerModuleInfo != null)
            {
                lock (runspaceLock)
                {
                    using (var ps = System.Management.Automation.PowerShell.Create())
                    {
                        ps.Runspace = this.analysisRunspace;
                        var ruleObjects = ps.AddCommand("Get-ScriptAnalyzerRule").Invoke();
                        foreach (var rule in ruleObjects)
                        {
                            ruleNames.Add((string)rule.Members["RuleName"].Value);
                        }
                    }
                }
            }

            return ruleNames;
        }

        /// <summary>
        /// Construct a PSScriptAnalyzer settings hashtable
        /// </summary>
        /// <param name="ruleSettingsMap">A settings hashtable</param>
        /// <returns></returns>
        public Hashtable GetPSSASettingsHashtable(IDictionary<string, Hashtable> ruleSettingsMap)
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
        /// Disposes the runspace being used by the analysis service.
        /// </summary>
        public void Dispose()
        {
            if (this.analysisRunspace != null)
            {
                this.analysisRunspace.Close();
                this.analysisRunspace.Dispose();
                this.analysisRunspace = null;
            }
        }

        #endregion // public methods

        #region Private Methods

        private ScriptFileMarker[] GetSemanticMarkers<TSettings>(
            ScriptFile file,
            string[] rules,
            TSettings settings) where TSettings : class
        {
            if (this.scriptAnalyzerModuleInfo != null
                && file.IsAnalysisEnabled
                && (typeof(TSettings) == typeof(string) || typeof(TSettings) == typeof(Hashtable))
                && (rules != null || settings != null))
            {
                // TODO: This is a temporary fix until we can change how
                // ScriptAnalyzer invokes their async tasks.
                // TODO: Make this async
                Task<ScriptFileMarker[]> analysisTask =
                    Task.Factory.StartNew<ScriptFileMarker[]>(
                        () =>
                        {
                            return
                                 GetDiagnosticRecords(file, rules, settings)
                                .Select(ScriptFileMarker.FromDiagnosticRecord)
                                .ToArray();
                        },
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskScheduler.Default);
                analysisTask.Wait();
                return analysisTask.Result;
            }
            else
            {
                // Return an empty marker list
                return new ScriptFileMarker[0];
            }
        }

        private void FindPSScriptAnalyzer()
        {
            lock (runspaceLock)
            {
                using (var ps = System.Management.Automation.PowerShell.Create())
                {
                    ps.Runspace = this.analysisRunspace;

                    var modules = ps.AddCommand("Get-Module")
                        .AddParameter("List")
                        .AddParameter("Name", "PSScriptAnalyzer")
                        .Invoke();

                    var psModule = modules == null ? null : modules.FirstOrDefault();
                    if (psModule != null)
                    {
                        scriptAnalyzerModuleInfo = psModule.ImmediateBaseObject as PSModuleInfo;
                        Logger.Write(
                            LogLevel.Normal,
                                string.Format(
                                    "PSScriptAnalyzer found at {0}",
                                    scriptAnalyzerModuleInfo.Path));
                    }
                    else
                    {
                        Logger.Write(
                            LogLevel.Normal,
                            "PSScriptAnalyzer module was not found.");
                    }
                }
            }
        }

        private void ImportPSScriptAnalyzer()
        {
            if (scriptAnalyzerModuleInfo != null)
            {
                lock (runspaceLock)
                {
                    using (var ps = System.Management.Automation.PowerShell.Create())
                    {
                        ps.Runspace = this.analysisRunspace;

                        var module = ps.AddCommand("Import-Module")
                            .AddParameter("ModuleInfo", scriptAnalyzerModuleInfo)
                            .AddParameter("PassThru")
                            .Invoke();

                        if (module == null)
                        {
                            this.scriptAnalyzerModuleInfo = null;
                            Logger.Write(LogLevel.Warning,
                                String.Format("Cannot Import PSScriptAnalyzer: {0}"));
                        }
                        else
                        {
                            Logger.Write(LogLevel.Normal,
                                String.Format("Successfully imported PSScriptAnalyzer"));
                        }
                    }
                }
            }
        }

        private void EnumeratePSScriptAnalyzerRules()
        {
            if (scriptAnalyzerModuleInfo != null)
            {
                var rules = GetPSScriptAnalyzerRules();
                var sb = new StringBuilder();
                sb.AppendLine("Available PSScriptAnalyzer Rules:");
                foreach (var rule in rules)
                {
                    sb.AppendLine(rule);
                }

                Logger.Write(LogLevel.Verbose, sb.ToString());
            }
        }

        private void InitializePSScriptAnalyzer()
        {
            FindPSScriptAnalyzer();
            ImportPSScriptAnalyzer();
            EnumeratePSScriptAnalyzerRules();
        }

        private IEnumerable<PSObject> GetDiagnosticRecords(ScriptFile file)
        {
            return GetDiagnosticRecords(file, this.activeRules, this.settingsPath);
        }

        // TSettings can either be of type Hashtable or string
        // as scriptanalyzer settings parameter takes either a hashtable or string
        private IEnumerable<PSObject> GetDiagnosticRecords<TSettings>(
            ScriptFile file,
            string[] rules,
            TSettings settings) where TSettings: class
        {
            var task = GetDiagnosticRecordsAsync(file, rules, settings);
            task.Wait();
            return task.Result;
        }

        private async Task<IEnumerable<PSObject>> GetDiagnosticRecordsAsync<TSettings>(
            ScriptFile file,
            string[] rules,
            TSettings settings) where TSettings : class
        {
            IEnumerable<PSObject> diagnosticRecords = Enumerable.Empty<PSObject>();

            if (this.scriptAnalyzerModuleInfo != null
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
                        { "ScriptDefinition", file.Contents },
                        { settingParameter, settingArgument }
                    });
            }

            Logger.Write(
                LogLevel.Verbose,
                String.Format("Found {0} violations", diagnosticRecords.Count()));

            return diagnosticRecords;
        }

        private async Task<IEnumerable<PSObject>> InvokePowerShellAsync(string command, IDictionary<string, object> paramArgMap)
        {
            var task = Task.Run(() =>
            {
                using (var powerShell = System.Management.Automation.PowerShell.Create())
                {
                    powerShell.Runspace = this.analysisRunspace;
                    powerShell.AddCommand(command);
                    foreach (var kvp in paramArgMap)
                    {
                        powerShell.AddParameter(kvp.Key, kvp.Value);
                    }

                    var powerShellCommandResult = powerShell.BeginInvoke();
                    var result = powerShell.EndInvoke(powerShellCommandResult);

                    if (result == null)
                    {
                        return Enumerable.Empty<PSObject>();
                    }

                    return result;
                }
            });

            await task;
            return task.Result;
        }

        #endregion //private methods
    }
}
