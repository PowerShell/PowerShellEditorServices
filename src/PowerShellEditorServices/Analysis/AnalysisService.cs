﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Console;
using System.Management.Automation;
using System.Collections.Generic;
using System.Text;

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

        /// <summary>
        /// Defines the list of Script Analyzer rules to include by default if
        /// no settings file is specified.
        /// </summary>
        private static readonly string[] IncludedRules = {
            "PSUseApprovedVerbs",
            "PSReservedCmdletChar",
            "PSReservedParams",
            "PSShouldProcess",
            "PSMissingModuleManifestField",
            "PSAvoidDefaultValueSwitchParameter",
            "PSUseDeclaredVarsMoreThanAssigments"
        };

        #endregion // Private Fields

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
                this.analysisRunspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
                this.analysisRunspace.ThreadOptions = PSThreadOptions.ReuseThread;
                this.analysisRunspace.Open();
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
        /// Performs semantic analysis on the given ScriptFile and returns
        /// an array of ScriptFileMarkers.
        /// </summary>
        /// <param name="file">The ScriptFile which will be analyzed for semantic markers.</param>
        /// <returns>An array of ScriptFileMarkers containing semantic analysis results.</returns>
        public ScriptFileMarker[] GetSemanticMarkers(ScriptFile file)
        {
            if (this.scriptAnalyzerModuleInfo != null && file.IsAnalysisEnabled)
            {
                // TODO: This is a temporary fix until we can change how
                // ScriptAnalyzer invokes their async tasks.
                // TODO: Make this async
                Task<ScriptFileMarker[]> analysisTask =
                    Task.Factory.StartNew<ScriptFileMarker[]>(
                        () =>
                        {
                                return 
                                     GetDiagnosticRecords(file) 
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
        private void FindPSScriptAnalyzer()
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

        private void ImportPSScriptAnalyzer()
        {
            if (scriptAnalyzerModuleInfo != null)
            {
                using (var ps = System.Management.Automation.PowerShell.Create())
                {
                    ps.Runspace = this.analysisRunspace;
                    var module = ps.AddCommand("Import-Module").
                        AddParameter("ModuleInfo", scriptAnalyzerModuleInfo)
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

        private void EnumeratePSScriptAnalyzerRules()
        {
            if (scriptAnalyzerModuleInfo != null)
            {
                using (var ps = System.Management.Automation.PowerShell.Create())
                {
                    ps.Runspace = this.analysisRunspace;
                    var rules = ps.AddCommand("Get-ScriptAnalyzerRule").Invoke();
                    var sb = new StringBuilder();
                    sb.AppendLine("Available PSScriptAnalyzer Rules:");
                    foreach (var rule in rules)
                    {
                        sb.AppendLine((string)rule.Members["RuleName"].Value);
                    }
                    Logger.Write(LogLevel.Verbose, sb.ToString());
                }
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
            IEnumerable<PSObject> diagnosticRecords = Enumerable.Empty<PSObject>();
            if (this.scriptAnalyzerModuleInfo != null)
            {
                using (var ps = System.Management.Automation.PowerShell.Create())
                {
                    ps.Runspace = this.analysisRunspace;
                    Logger.Write(
                        LogLevel.Verbose,
                        String.Format("Running PSScriptAnalyzer against {0}", file.FilePath));

                    // currently not working with include rules
                    diagnosticRecords = ps.AddCommand("Invoke-ScriptAnalyzer")
                        .AddParameter("ScriptDefinition", file.Contents)
                        .Invoke();
                }
            }
            Logger.Write(
                LogLevel.Verbose,
                String.Format("Found {0} violations", diagnosticRecords.Count()));
            return diagnosticRecords;
        }

        #endregion //private methods
    }
}
