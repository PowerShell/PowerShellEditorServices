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
    internal interface IAnalysisEngine : IDisposable
    {
        bool IsEnabled { get; }

        Task<string> FormatAsync(
            string scriptDefinition,
            Hashtable settings,
            int[] rangeList);

        Task<ScriptFileMarker[]> GetSemanticMarkersAsync(
            string scriptFileContent,
            Hashtable settings);

        Task<ScriptFileMarker[]> GetSemanticMarkersAsync(
            string scriptFileContent,
            string settingsFilePath);

        Task<ScriptFileMarker[]> GetSemanticMarkersAsync(
            string scriptFileContent,
            string[] rules);

    }

}
