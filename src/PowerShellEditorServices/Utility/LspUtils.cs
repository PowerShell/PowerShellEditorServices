// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class LspUtils
    {
        public static TextDocumentSelector PowerShellDocumentSelector => new(
            TextDocumentFilter.ForLanguage("powershell"),
            TextDocumentFilter.ForLanguage("pwsh"),

            // The vim extension sets all PowerShell files as language "ps1" so this
            // makes sure we track those.
            TextDocumentFilter.ForLanguage("ps1"),
            TextDocumentFilter.ForLanguage("psm1"),
            TextDocumentFilter.ForLanguage("psd1"),

            // Also specify the file extensions to be thorough
            // This won't handle untitled files which is why we have to do the ones above.
            TextDocumentFilter.ForPattern("**/*.ps*1")
        );
    }
}
