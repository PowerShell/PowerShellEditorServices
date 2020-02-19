//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.Configuration;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class ConfigurationService
    {
        // This probably needs some sort of lock... or maybe LanguageServerSettings needs it.
        public LanguageServerSettings CurrentSettings { get; } = new LanguageServerSettings();
    }
}
