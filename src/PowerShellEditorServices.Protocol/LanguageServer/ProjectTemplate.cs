//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Templates;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class NewProjectFromTemplateRequest
    {
        public static readonly
            RequestType<NewProjectFromTemplateRequest, NewProjectFromTemplateResponse> Type =
            RequestType<NewProjectFromTemplateRequest, NewProjectFromTemplateResponse>.Create("powerShell/newProjectFromTemplate");

        public string DestinationPath { get; set; }

        public string TemplatePath { get; set; }
    }

    public class NewProjectFromTemplateResponse
    {
        public bool CreationSuccessful { get; set; }
    }

    public class GetProjectTemplatesRequest
    {
        public static readonly
            RequestType<GetProjectTemplatesRequest, GetProjectTemplatesResponse> Type =
            RequestType<GetProjectTemplatesRequest, GetProjectTemplatesResponse>.Create("powerShell/getProjectTemplates");

        public bool IncludeInstalledModules { get; set; }
    }

    public class GetProjectTemplatesResponse
    {
        public bool NeedsModuleInstall { get; set; }

        public TemplateDetails[] Templates { get; set; }
    }
}
