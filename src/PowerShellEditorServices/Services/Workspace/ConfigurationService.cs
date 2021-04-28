// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.Configuration;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class ConfigurationService
    {
        // This probably needs some sort of lock... or maybe LanguageServerSettings needs it.
        public LanguageServerSettings CurrentSettings { get; } = new LanguageServerSettings();
    }
}
