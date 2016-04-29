//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class LanguageServerSettings
    {
        public bool EnableProfileLoading { get; set; }

        public ScriptAnalysisSettings ScriptAnalysis { get; set; }

        public LanguageServerSettings()
        {
            this.ScriptAnalysis = new ScriptAnalysisSettings();
        }

        public void Update(LanguageServerSettings settings, string workspaceRootPath)
        {
            if (settings != null)
            {
                this.EnableProfileLoading = settings.EnableProfileLoading;
                this.ScriptAnalysis.Update(settings.ScriptAnalysis, workspaceRootPath);
            }
        }
    }

    public class ScriptAnalysisSettings
    {
        public bool? Enable { get; set; }

        public string SettingsPath { get; set; }

        public ScriptAnalysisSettings()
        {
            this.Enable = true;
        }

        public void Update(ScriptAnalysisSettings settings, string workspaceRootPath)
        {
            if (settings != null)
            {
                this.Enable = settings.Enable;

                string settingsPath = settings.SettingsPath;

                if (string.IsNullOrWhiteSpace(settingsPath))
                {
                    settingsPath = null;
                }
                else if (!Path.IsPathRooted(settingsPath))
                {
                    if (string.IsNullOrEmpty(workspaceRootPath))
                    {
                        // The workspace root path could be an empty string
                        // when the user has opened a PowerShell script file
                        // without opening an entire folder (workspace) first.
                        // In this case we should just log an error and let
                        // the specified settings path go through even though
                        // it will fail to load.
                        Logger.Write(
                            LogLevel.Error,
                            "Could not resolve Script Analyzer settings path due to null or empty workspaceRootPath.");
                    }
                    else
                    {
                        settingsPath = Path.GetFullPath(Path.Combine(workspaceRootPath, settingsPath));
                    }
                }

                this.SettingsPath = settingsPath;
            }
        }
    }

    public class LanguageServerSettingsWrapper
    {
        // NOTE: This property is capitalized as 'Powershell' because the
        // mode name sent from the client is written as 'powershell' and
        // JSON.net is using camelCasing.

        public LanguageServerSettings Powershell { get; set; }
    }
}
