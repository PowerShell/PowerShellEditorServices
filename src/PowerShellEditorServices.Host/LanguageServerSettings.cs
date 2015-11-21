//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Host
{
    internal class LanguageServerSettings
    {
        public ScriptAnalysisSettings ScriptAnalysis { get; set; }

        public LanguageServerSettings()
        {
            this.ScriptAnalysis = new ScriptAnalysisSettings();
        }

        public void Update(LanguageServerSettings settings)
        {
            if (settings != null)
            {
                this.ScriptAnalysis.Update(settings.ScriptAnalysis);
            }
        }
    }

    internal class ScriptAnalysisSettings
    {
        public bool? Enable { get; set; }

        public ScriptAnalysisSettings()
        {
            this.Enable = true;
        }

        public void Update(ScriptAnalysisSettings settings)
        {
            if (settings != null)
            {
                this.Enable = settings.Enable;
            }
        }
    }

    internal class SettingsWrapper
    {
        // NOTE: This property is capitalized as 'Powershell' because the
        // mode name sent from the client is written as 'powershell' and
        // JSON.net is using camelCasing.

        public LanguageServerSettings Powershell { get; set; }
    }
}
