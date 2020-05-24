//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class TemplateHandlers : IGetProjectTemplatesHandler, INewProjectFromTemplateHandler
    {
        private readonly ILogger<TemplateHandlers> _logger;
        private readonly TemplateService _templateService;

        public TemplateHandlers(
            ILoggerFactory factory,
            TemplateService templateService)
        {
            _logger = factory.CreateLogger<TemplateHandlers>();
            _templateService = templateService;
        }

        public async Task<GetProjectTemplatesResponse> Handle(GetProjectTemplatesRequest request, CancellationToken cancellationToken)
        {
            bool plasterInstalled = await _templateService.ImportPlasterIfInstalledAsync().ConfigureAwait(false);

            if (plasterInstalled)
            {
                var availableTemplates =
                    await _templateService.GetAvailableTemplatesAsync(
                        request.IncludeInstalledModules).ConfigureAwait(false);


                return new GetProjectTemplatesResponse
                {
                    Templates = availableTemplates
                };
            }

            return new GetProjectTemplatesResponse
            {
                NeedsModuleInstall = true,
                Templates = Array.Empty<TemplateDetails>()
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Want to log any exception")]
        public async Task<NewProjectFromTemplateResponse> Handle(NewProjectFromTemplateRequest request, CancellationToken cancellationToken)
        {
            bool creationSuccessful;
            try
            {
                await _templateService.CreateFromTemplateAsync(request.TemplatePath, request.DestinationPath).ConfigureAwait(false);
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
