//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;
using Microsoft.PowerShell.EditorServices.Templates;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    public class TemplateHandlers : IGetProjectTemplatesHandler, INewProjectFromTemplateHandler
    {
        private readonly ILogger<GetVersionHandler> _logger;
        private readonly TemplateService _templateService;

        public TemplateHandlers(
            ILoggerFactory factory,
            TemplateService templateService)
        {
            _logger = factory.CreateLogger<GetVersionHandler>();
            _templateService = templateService;
        }

        public async Task<GetProjectTemplatesResponse> Handle(GetProjectTemplatesRequest request, CancellationToken cancellationToken)
        {
            bool plasterInstalled = await _templateService.ImportPlasterIfInstalledAsync();

            if (plasterInstalled)
            {
                var availableTemplates =
                    await _templateService.GetAvailableTemplatesAsync(
                        request.IncludeInstalledModules);


                return new GetProjectTemplatesResponse
                {
                    Templates = availableTemplates
                };
            }
            
            return new GetProjectTemplatesResponse
            {
                NeedsModuleInstall = true,
                Templates = new TemplateDetails[0]
            };
        }

        public async Task<NewProjectFromTemplateResponse> Handle(NewProjectFromTemplateRequest request, CancellationToken cancellationToken)
        {
            bool creationSuccessful;
            try
            {
                await _templateService.CreateFromTemplateAsync(request.TemplatePath, request.DestinationPath);
                creationSuccessful = true;
            }
            catch (Exception e)
            {
                // We don't really care if this worked or not but we report status.
                _logger.LogException("New plaster template failed.", e);
                creationSuccessful = false;
            }

            return new NewProjectFromTemplateResponse
            {
                CreationSuccessful = creationSuccessful
            };
        }
    }
}
