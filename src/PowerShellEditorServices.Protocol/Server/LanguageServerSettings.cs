//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class LanguageServerSettings
    {
        public bool LoadProfiles { get; set; }

        public ScriptAnalysisSettings ScriptAnalysis { get; set; }

        public LanguageServerSettings()
        {
            this.ScriptAnalysis = new ScriptAnalysisSettings();
        }

        public void Update(LanguageServerSettings settings)
        {
            if (settings != null)
            {
                this.LoadProfiles = settings.LoadProfiles;
                this.ScriptAnalysis.Update(settings.ScriptAnalysis);
            }
        }
    }

    public class ScriptAnalysisSettings
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

    public class LanguageServerSettingsWrapper
    {
        // NOTE: This property is capitalized as 'Powershell' because the
        // mode name sent from the client is written as 'powershell' and
        // JSON.net is using camelCasing.

        public LanguageServerSettings Powershell { get; set; }
    }
}
