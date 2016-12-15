//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Templates
{
    /// <summary>
    /// Provides a service for listing PowerShell project templates and creating
    /// new projects from those templates.  This service leverages the Plaster
    /// module for creating projects from templates.
    /// </summary>
    public class TemplateService
    {
        #region Private Fields

        private bool isPlasterLoaded;
        private bool? isPlasterInstalled;
        private PowerShellContext powerShellContext;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the TemplateService class.
        /// </summary>
        /// <param name="powerShellContext">The PowerShellContext to use for this service.</param>
        public TemplateService(PowerShellContext powerShellContext)
        {
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            this.powerShellContext = powerShellContext;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if Plaster is installed on the user's machine.
        /// </summary>
        /// <returns>A Task that can be awaited until the check is complete.  The result will be true if Plaster is installed.</returns>
        public async Task<bool> ImportPlasterIfInstalled()
        {
            if (!this.isPlasterInstalled.HasValue)
            {
                PSCommand psCommand = new PSCommand();
                psCommand.AddCommand("Get-Module");
                psCommand.AddParameter("ListAvailable");
                psCommand.AddParameter("Name", "Plaster");

                Logger.Write(LogLevel.Verbose, "Checking if Plaster is installed...");

                var getResult =
                    await this.powerShellContext.ExecuteCommand<object>(
                        psCommand, false, false);

                this.isPlasterInstalled = getResult.Any();
                string installedQualifier = 
                    this.isPlasterInstalled.Value
                        ? string.Empty : "not ";

                Logger.Write(
                    LogLevel.Verbose,
                    $"Plaster is {installedQualifier}installed!");

                // Attempt to load plaster
                if (this.isPlasterInstalled.Value && this.isPlasterLoaded == false)
                {
                    Logger.Write(LogLevel.Verbose, "Loading Plaster...");

                    psCommand = new PSCommand();
                    psCommand.AddCommand("Import-Module");
                    psCommand.AddParameter("Name", "Plaster");
                    psCommand.AddParameter("PassThru");

                    var importResult =
                        await this.powerShellContext.ExecuteCommand<object>(
                            psCommand, false, false);

                    this.isPlasterLoaded = importResult.Any();
                    string loadedQualifier =
                        this.isPlasterInstalled.Value
                            ? "was" : "could not be";

                    Logger.Write(
                        LogLevel.Verbose,
                        $"Plaster {loadedQualifier} loaded successfully!");
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
        public async Task<TemplateDetails[]> GetAvailableTemplates(
            bool includeInstalledModules)
        {
            if (!this.isPlasterLoaded)
            {
                throw new InvalidOperationException("Plaster is not loaded, templates cannot be accessed.");
            };

            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand("Get-PlasterTemplate");

            if (includeInstalledModules)
            {
                psCommand.AddParameter("IncludeModules");
            }

            var templateObjects =
                await this.powerShellContext.ExecuteCommand<PSObject>(
                    psCommand, false, false);

            Logger.Write(
                LogLevel.Verbose,
                $"Found {templateObjects.Count()} Plaster templates");

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
        public async Task<bool> CreateFromTemplate(
            string templatePath,
            string destinationPath)
        {
            Logger.Write(
                LogLevel.Verbose,
                $"Invoking Plaster...\n\n    TemplatePath: {templatePath}\n    DestinationPath: {destinationPath}");

            PSCommand command = new PSCommand();
            command.AddCommand("Invoke-Plaster");
            command.AddParameter("TemplatePath", templatePath);
            command.AddParameter("DestinationPath", destinationPath);

            var errorString = new System.Text.StringBuilder();
            await this.powerShellContext.ExecuteCommand<PSObject>(
                command, errorString, false, true);

            // If any errors were written out, creation was not successful
            return errorString.Length == 0;
        }

        #endregion

        #region Private Methods

        private static TemplateDetails CreateTemplateDetails(PSObject psObject)
        {
            object[] tags = psObject.Members["Tags"].Value as object[];

            return new TemplateDetails
            {
                Title = psObject.Members["Title"].Value as string,
                Author = psObject.Members["Author"].Value as string,
                Version = psObject.Members["Version"].Value.ToString(),
                Description = psObject.Members["Description"].Value as string,
                TemplatePath = psObject.Members["TemplatePath"].Value as string,
                Tags =
                    tags != null
                    ? string.Join(", ", tags)
                    : string.Empty
            };
        }

        #endregion
    }
}
