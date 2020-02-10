//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Provides a service for listing PowerShell project templates and creating
    /// new projects from those templates.  This service leverages the Plaster
    /// module for creating projects from templates.
    /// </summary>
    internal class TemplateService
    {
        #region Private Fields

        private readonly ILogger logger;
        private bool isPlasterLoaded;
        private bool? isPlasterInstalled;
        private readonly PowerShellContextService powerShellContext;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the TemplateService class.
        /// </summary>
        /// <param name="powerShellContext">The PowerShellContext to use for this service.</param>
        /// <param name="factory">An ILoggerFactory implementation used for writing log messages.</param>
        public TemplateService(PowerShellContextService powerShellContext, ILoggerFactory factory)
        {
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            this.logger = factory.CreateLogger<TemplateService>();
            this.powerShellContext = powerShellContext;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if Plaster is installed on the user's machine.
        /// </summary>
        /// <returns>A Task that can be awaited until the check is complete.  The result will be true if Plaster is installed.</returns>
        public async Task<bool> ImportPlasterIfInstalledAsync()
        {
            if (!this.isPlasterInstalled.HasValue)
            {
                PSCommand psCommand = new PSCommand();

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

                this.logger.LogTrace("Checking if Plaster is installed...");

                var getResult =
                    await this.powerShellContext.ExecuteCommandAsync<PSObject>(
                        psCommand, false, false).ConfigureAwait(false);

                PSObject moduleObject = getResult.First();
                this.isPlasterInstalled = moduleObject != null;
                string installedQualifier =
                    this.isPlasterInstalled.Value
                        ? string.Empty : "not ";

                this.logger.LogTrace($"Plaster is {installedQualifier}installed!");

                // Attempt to load plaster
                if (this.isPlasterInstalled.Value && this.isPlasterLoaded == false)
                {
                    this.logger.LogTrace("Loading Plaster...");

                    psCommand = new PSCommand();
                    psCommand
                        .AddCommand("Import-Module")
                        .AddParameter("ModuleInfo", (PSModuleInfo)moduleObject.ImmediateBaseObject)
                        .AddParameter("PassThru");

                    var importResult =
                        await this.powerShellContext.ExecuteCommandAsync<object>(
                            psCommand, false, false).ConfigureAwait(false);

                    this.isPlasterLoaded = importResult.Any();
                    string loadedQualifier =
                        this.isPlasterInstalled.Value
                            ? "was" : "could not be";

                    this.logger.LogTrace($"Plaster {loadedQualifier} loaded successfully!");
                }
            }

            return this.isPlasterInstalled.Value;
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
            if (!this.isPlasterLoaded)
            {
                throw new InvalidOperationException("Plaster is not loaded, templates cannot be accessed.");
            }

            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand("Get-PlasterTemplate");

            if (includeInstalledModules)
            {
                psCommand.AddParameter("IncludeModules");
            }

            var templateObjects =
                await this.powerShellContext.ExecuteCommandAsync<PSObject>(
                    psCommand, false, false).ConfigureAwait(false);

            this.logger.LogTrace($"Found {templateObjects.Count()} Plaster templates");

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
            this.logger.LogTrace(
                $"Invoking Plaster...\n\n    TemplatePath: {templatePath}\n    DestinationPath: {destinationPath}");

            PSCommand command = new PSCommand();
            command.AddCommand("Invoke-Plaster");
            command.AddParameter("TemplatePath", templatePath);
            command.AddParameter("DestinationPath", destinationPath);

            var errorString = new System.Text.StringBuilder();
            await this.powerShellContext.ExecuteCommandAsync<PSObject>(
                command,
                errorString,
                new ExecutionOptions
                {
                    WriteOutputToHost = false,
                    WriteErrorsToHost = true,
                    InterruptCommandPrompt = true
                }).ConfigureAwait(false);

            // If any errors were written out, creation was not successful
            return errorString.Length == 0;
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
