//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.Windows.PowerShell.ScriptAnalyzer;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Console;

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
        private ScriptAnalyzer scriptAnalyzer;

        /// <summary>
        /// Defines the list of Script Analyzer rules to include by default if
        /// no settings file can be found.
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
                // Attempt to create a ScriptAnalyzer instance first
                // just in case the assembly can't be found and we
                // can skip creating an extra runspace.
                this.scriptAnalyzer = new ScriptAnalyzer();

                this.analysisRunspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
                this.analysisRunspace.ApartmentState = ApartmentState.STA;
                this.analysisRunspace.ThreadOptions = PSThreadOptions.ReuseThread;
                this.analysisRunspace.Open();

                this.scriptAnalyzer.Initialize(
                    this.analysisRunspace,
                    new AnalysisOutputWriter(consoleHost),
                    includeRuleNames: settingsPath == null ? IncludedRules : null,
                    includeDefaultRules: true,
                    profile: settingsPath);
            }
            catch (FileNotFoundException)
            {
                Logger.Write(
                    LogLevel.Warning,
                    "Script Analyzer binaries not found, AnalysisService will be disabled.");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Performs semantic analysis on the given ScriptFile and returns
        /// an array of ScriptFileMarkers.
        /// </summary>
        /// <param name="file">The ScriptFile which will be analyzed for semantic markers.</param>
        /// <returns>An array of ScriptFileMarkers containing semantic analysis results.</returns>
        public ScriptFileMarker[] GetSemanticMarkers(ScriptFile file)
        {
            if (this.scriptAnalyzer != null && file.IsAnalysisEnabled)
            {
                // TODO: This is a temporary fix until we can change how
                // ScriptAnalyzer invokes their async tasks.
                Task<ScriptFileMarker[]> analysisTask =
                    Task.Factory.StartNew<ScriptFileMarker[]>(
                        () =>
                        {
                            return 
                                this.scriptAnalyzer
                                    .AnalyzeSyntaxTree(
                                        file.ScriptAst,
                                        file.ScriptTokens,
                                        file.FilePath)
                                    .Select(ScriptFileMarker.FromDiagnosticRecord)
                                    .ToArray();
                        },
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskScheduler.Default);

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

        #endregion
    }
}
