//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    public class CompleteCommandFromModule
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
                StartLineNumber = 13,
                StartColumnNumber = 11
            };

        public static readonly CompletionDetails ExpectedCompletion =
            CompletionDetails.Create(
                "Import-Module",
                CompletionType.Command,
                "Import-Module [-Name] <string[]> [-Global] [-Prefix <string>] [-Function <string[]>] [-Cmdlet <string[]>] [-Variable <string[]>] [-Alias <string[]>] [-Force] [-PassThru] [-AsCustomObject] [-MinimumVersion <version>] [-MaximumVersion <string>] [-RequiredVersion <version>] [-ArgumentList <Object[]>] [-DisableNameChecking] [-NoClobber] [-Scope <string>] [<CommonParameters>]\n\nImport-Module [-Name] <string[]> -PSSession <PSSession> [-Global] [-Prefix <string>] [-Function <string[]>] [-Cmdlet <string[]>] [-Variable <string[]>] [-Alias <string[]>] [-Force] [-PassThru] [-AsCustomObject] [-MinimumVersion <version>] [-MaximumVersion <string>] [-RequiredVersion <version>] [-ArgumentList <Object[]>] [-DisableNameChecking] [-NoClobber] [-Scope <string>] [<CommonParameters>]\n\nImport-Module [-Name] <string[]> -CimSession <CimSession> [-Global] [-Prefix <string>] [-Function <string[]>] [-Cmdlet <string[]>] [-Variable <string[]>] [-Alias <string[]>] [-Force] [-PassThru] [-AsCustomObject] [-MinimumVersion <version>] [-MaximumVersion <string>] [-RequiredVersion <version>] [-ArgumentList <Object[]>] [-DisableNameChecking] [-NoClobber] [-Scope <string>] [-CimResourceUri <uri>] [-CimNamespace <string>] [<CommonParameters>]\n\nImport-Module [-FullyQualifiedName] <ModuleSpecification[]> [-Global] [-Prefix <string>] [-Function <string[]>] [-Cmdlet <string[]>] [-Variable <string[]>] [-Alias <string[]>] [-Force] [-PassThru] [-AsCustomObject] [-ArgumentList <Object[]>] [-DisableNameChecking] [-NoClobber] [-Scope <string>] [<CommonParameters>]\n\nImport-Module [-FullyQualifiedName] <ModuleSpecification[]> -PSSession <PSSession> [-Global] [-Prefix <string>] [-Function <string[]>] [-Cmdlet <string[]>] [-Variable <string[]>] [-Alias <string[]>] [-Force] [-PassThru] [-AsCustomObject] [-ArgumentList <Object[]>] [-DisableNameChecking] [-NoClobber] [-Scope <string>] [<CommonParameters>]\n\nImport-Module [-Assembly] <Assembly[]> [-Global] [-Prefix <string>] [-Function <string[]>] [-Cmdlet <string[]>] [-Variable <string[]>] [-Alias <string[]>] [-Force] [-PassThru] [-AsCustomObject] [-ArgumentList <Object[]>] [-DisableNameChecking] [-NoClobber] [-Scope <string>] [<CommonParameters>]\n\nImport-Module [-ModuleInfo] <psmoduleinfo[]> [-Global] [-Prefix <string>] [-Function <string[]>] [-Cmdlet <string[]>] [-Variable <string[]>] [-Alias <string[]>] [-Force] [-PassThru] [-AsCustomObject] [-ArgumentList <Object[]>] [-DisableNameChecking] [-NoClobber] [-Scope <string>] [<CommonParameters>]");
    }
}
