//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.Windows.PowerShell.ScriptAnalyzer;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Analysis
{
    /// <summary>
    /// Provides a high-level service for performing semantic analysis
    /// of PowerShell scripts.
    /// </summary>
    public class AnalysisService
    {
        #region Private Fields

        private Runspace runspace;
        private ScriptAnalyzer scriptAnalyzer;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the AnalysisService class with a
        /// Runspace to use for analysis operations.
        /// </summary>
        /// <param name="analysisRunspace">
        /// The Runspace in which analysis operations will be performed.
        /// </param>
        public AnalysisService(Runspace analysisRunspace)
        {
            this.runspace = analysisRunspace;
            this.scriptAnalyzer = new ScriptAnalyzer();
            this.scriptAnalyzer.Initialize(
                analysisRunspace,
                new AnalysisOutputWriter());
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

        #endregion
    }
}
