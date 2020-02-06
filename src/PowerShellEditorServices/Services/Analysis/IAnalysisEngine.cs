//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using System;
using System.Collections;
using System.Collections.Generic;
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
        /// <param name="formatSettings">The settings to apply when formatting.</param>
        /// <param name="rangeList">An optional list of ranges to format in the script.</param>
        /// <returns></returns>
        Task<string> FormatAsync(
            string scriptDefinition,
            Hashtable formatSettings,
            int[] rangeList);

        /// <summary>
        /// Analyze a PowerShell script file using a settings object.
        /// </summary>
        /// <param name="scriptFileContent">The script to analyze in string form.</param>
        /// <returns>Markers for any diagnostics in the script.</returns>
        Task<ScriptFileMarker[]> AnalyzeScriptAsync(string scriptFileContent);

        /// <summary>
        /// Analyze a PowerShell script file using a settings object.
        /// </summary>
        /// <param name="scriptFileContent">The script to analyze in string form.</param>
        /// <param name="settings">A settings object to use as engine settings in this call.</param>
        /// <returns>Markers for any diagnostics in the script.</returns>
        Task<ScriptFileMarker[]> AnalyzeScriptAsync(string scriptFileContent, Hashtable settings);
    }

}
