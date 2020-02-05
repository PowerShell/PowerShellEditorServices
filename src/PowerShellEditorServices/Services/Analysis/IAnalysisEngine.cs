//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using System;
using System.Collections;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.Analysis
{
    /// <summary>
    /// An engine to run PowerShell script analysis.
    /// </summary>
    internal interface IAnalysisEngine : IDisposable
    {
        /// <summary>
        /// If false, other methods on this object will have no meaningful implementation.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Format a PowerShell script.
        /// </summary>
        /// <param name="scriptDefinition">The string of the PowerShell script to format.</param>
        /// <param name="settings">The formatter settings.</param>
        /// <param name="rangeList">An optional list of ranges to format in the script.</param>
        /// <returns></returns>
        Task<string> FormatAsync(
            string scriptDefinition,
            Hashtable settings,
            int[] rangeList);

        /// <summary>
        /// Analyze a PowerShell script file using a settings object.
        /// </summary>
        /// <param name="scriptFileContent">The script to analyze in string form.</param>
        /// <param name="settings">The settings hashtable to configure the analyzer with.</param>
        /// <returns>Markers for any diagnostics in the script.</returns>
        Task<ScriptFileMarker[]> AnalyzeScriptAsync(
            string scriptFileContent,
            Hashtable settings);

        /// <summary>
        /// Analyze a PowerShell script file with a path to a settings file.
        /// </summary>
        /// <param name="scriptFileContent">The script to analyze in string form.</param>
        /// <param name="settingsFilePath">The path to the settings file with which to configure the analyzer.</param>
        /// <returns>Markers for any diagnostics in the script.</returns>
        Task<ScriptFileMarker[]> AnalyzeScriptAsync(
            string scriptFileContent,
            string settingsFilePath);

        /// <summary>
        /// Analyze a PowerShell script file using a set of script analysis rules.
        /// </summary>
        /// <param name="scriptFileContent">The script to analyze in string form.</param>
        /// <param name="rules">The rules to run on the script for analysis.</param>
        /// <returns>Markers for any diagnostics in the script.</returns>
        Task<ScriptFileMarker[]> AnalyzeScriptAsync(
            string scriptFileContent,
            string[] rules);

    }

}
