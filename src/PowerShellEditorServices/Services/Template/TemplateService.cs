// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.Template
{
    /// <summary>
    /// Provides a service for listing PowerShell project templates and creating
    /// new projects from those templates.  This service leverages the Plaster
    /// module for creating projects from templates.
    /// </summary>
    internal class TemplateService
    {
        #region Private Fields

        private readonly ILogger _logger;
        private bool isPlasterLoaded;
        private bool? isPlasterInstalled;
        private readonly IInternalPowerShellExecutionService _executionService;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the TemplateService class.
        /// </summary>
        /// <param name="executionService">The PowerShellContext to use for this service.</param>
        /// <param name="factory">An ILoggerFactory implementation used for writing log messages.</param>
        public TemplateService(IInternalPowerShellExecutionService executionService, ILoggerFactory factory)
        {
            _logger = factory.CreateLogger<TemplateService>();
            _executionService = executionService;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if Plaster is installed on the user's machine.
        /// </summary>
        /// <returns>A Task that can be awaited until the check is complete.  The result will be true if Plaster is installed.</returns>
        public async Task<bool> ImportPlasterIfInstalledAsync()
        {
            if (!isPlasterInstalled.HasValue)
            {
                PSCommand psCommand = new();

                psCommand
                    .AddCommand("Get-Module")
                    .AddParameter("ListAvailable")
                    .AddParameter("Name", "Plaster");

                psCommand
                    .AddCommand("Sort-Object")
                    .AddParameter("Descending")
                    .AddParameter("Property", "Version");

                psCommand
                    .AddCommand("Select-Object")
                    .AddParameter("First", 1);

                _logger.LogTrace("Checking if Plaster is installed...");

                PSObject moduleObject = (await _executionService.ExecutePSCommandAsync<PSObject>(psCommand, CancellationToken.None).ConfigureAwait(false))[0];

                isPlasterInstalled = moduleObject != null;
                string installedQualifier =
                    isPlasterInstalled.Value
                        ? string.Empty : "not ";

                _logger.LogTrace($"Plaster is {installedQualifier}installed!");

                // Attempt to load plaster
                if (isPlasterInstalled.Value && !isPlasterLoaded)
                {
                    _logger.LogTrace("Loading Plaster...");

                    psCommand = new PSCommand();
                    psCommand
                        .AddCommand("Import-Module")
                        .AddParameter("ModuleInfo", (PSModuleInfo)moduleObject.ImmediateBaseObject)
                        .AddParameter("PassThru");

                    IReadOnlyList<PSModuleInfo> importResult = await _executionService.ExecutePSCommandAsync<PSModuleInfo>(psCommand, CancellationToken.None).ConfigureAwait(false);

                    isPlasterLoaded = importResult.Count > 0;
                    string loadedQualifier =
                        isPlasterInstalled.Value
                            ? "was" : "could not be";

                    _logger.LogTrace($"Plaster {loadedQualifier} loaded successfully!");
                }
            }

            return isPlasterInstalled.Value;
        }

        /// <summary>
        /// Gets the available file or project templates on the user's
        /// machine.
        /// </summary>
        /// <param name="includeInstalledModules">
        /// If true, searches the user's installed PowerShell modules for
        /// included templates.
        /// </param>
        /// <returns>A Task which can be awaited for the TemplateDetails list to be returned.</returns>
        public async Task<TemplateDetails[]> GetAvailableTemplatesAsync(
            bool includeInstalledModules)
        {
            if (!isPlasterLoaded)
            {
                throw new InvalidOperationException("Plaster is not loaded, templates cannot be accessed.");
            }

            PSCommand psCommand = new();
            psCommand.AddCommand("Get-PlasterTemplate");

            if (includeInstalledModules)
            {
                psCommand.AddParameter("IncludeModules");
            }

            IReadOnlyList<PSObject> templateObjects = await _executionService.ExecutePSCommandAsync<PSObject>(
                psCommand,
                CancellationToken.None).ConfigureAwait(false);

            _logger.LogTrace($"Found {templateObjects.Count} Plaster templates");

            return
                templateObjects
                    .Select(CreateTemplateDetails)
                    .ToArray();
        }

        /// <summary>
        /// Creates a new file or project from a specified template and
        /// places it in the destination path.  This ultimately calls
        /// Invoke-Plaster in PowerShell.
        /// </summary>
        /// <param name="templatePath">The folder path containing the template.</param>
        /// <param name="destinationPath">The folder path where the files will be created.</param>
        /// <returns>A boolean-returning Task which communicates success or failure.</returns>
        public async Task<bool> CreateFromTemplateAsync(
            string templatePath,
            string destinationPath)
        {
            _logger.LogTrace(
                $"Invoking Plaster...\n\n    TemplatePath: {templatePath}\n    DestinationPath: {destinationPath}");

            PSCommand command = new PSCommand()
                .AddCommand("Invoke-Plaster")
                .AddParameter("TemplatePath", templatePath)
                .AddParameter("DestinationPath", destinationPath);

            // This command is interactive so it requires the foreground.
            await _executionService.ExecutePSCommandAsync(
                command,
                CancellationToken.None,
                new PowerShellExecutionOptions
                {
                    RequiresForeground = true,
                    WriteOutputToHost = true,
                    ThrowOnError = false
                }).ConfigureAwait(false);

            // If any errors were written out, creation was not successful
            return true;
        }

        #endregion

        #region Private Methods

        private static TemplateDetails CreateTemplateDetails(PSObject psObject)
        {
            return new TemplateDetails
            {
                Title = psObject.Members["Title"].Value as string,
                Author = psObject.Members["Author"].Value as string,
                Version = psObject.Members["Version"].Value.ToString(),
                Description = psObject.Members["Description"].Value as string,
                TemplatePath = psObject.Members["TemplatePath"].Value as string,
                Tags =
                    psObject.Members["Tags"].Value is object[] tags
                    ? string.Join(", ", tags)
                    : string.Empty
            };
        }

        #endregion
    }
}
